using System.CommandLine;
using System.Text.Json;
using AppStoreConnectCli.Asc;
using AppStoreConnectCli.Asc.Assets;
using AppStoreConnectCli.Asc.Auth;
using AppStoreConnectCli.Models;
using AppStoreConnectCli.Reporting;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

// ── Configure Serilog ─────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/run_.log", rollingInterval: RollingInterval.Day,
                  outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

// ── CLI definition ────────────────────────────────────────────────────────────
var keyIdOption = new Option<string>("--keyid", "App Store Connect API key ID (e.g. AB12CD34EF)") { IsRequired = true };
var issuerIdOption = new Option<string>("--issuerid", "App Store Connect issuer ID (UUID)") { IsRequired = true };
var privateKeyOption = new Option<FileInfo>("--privatekey", "Path to the .p8 private key file") { IsRequired = true };
var appIdOption = new Option<string>("--appid", "App Store Connect app ID (numeric)") { IsRequired = true };
var configOption = new Option<FileInfo>("--config", "Path to the achievements JSON config file") { IsRequired = true };
var imageFolderOption = new Option<DirectoryInfo?>("--imagefolder", "Folder containing achievement image files");
var updateOption = new Option<bool>("--update", () => false, "Update existing achievements (default: skip)");

var rootCommand = new RootCommand("App Store Connect CLI — Game Center Achievements tool")
{
    keyIdOption,
    issuerIdOption,
    privateKeyOption,
    appIdOption,
    configOption,
    imageFolderOption,
    updateOption
};

rootCommand.SetHandler(async (context) =>
{
    var keyId = context.ParseResult.GetValueForOption(keyIdOption)!;
    var issuerId = context.ParseResult.GetValueForOption(issuerIdOption)!;
    var privateKeyFile = context.ParseResult.GetValueForOption(privateKeyOption)!;
    var appId = context.ParseResult.GetValueForOption(appIdOption)!;
    var configFile = context.ParseResult.GetValueForOption(configOption)!;
    var imageFolder = context.ParseResult.GetValueForOption(imageFolderOption);
    var update = context.ParseResult.GetValueForOption(updateOption);

    context.ExitCode = await RunAsync(keyId, issuerId, privateKeyFile.FullName, appId,
        configFile.FullName, imageFolder?.FullName, update, context.GetCancellationToken());
});

return await rootCommand.InvokeAsync(args);

// ── Main orchestration ────────────────────────────────────────────────────────
async Task<int> RunAsync(
    string keyId,
    string issuerId,
    string privateKeyPath,
    string appId,
    string configPath,
    string? imageFolder,
    bool update,
    CancellationToken ct)
{
    Log.Information("appstoreconnect-cli starting");

    // 1. Validate files
    if (!File.Exists(privateKeyPath))
    {
        Log.Fatal("Private key file not found: {Path}", privateKeyPath);
        return 2;
    }
    if (!File.Exists(configPath))
    {
        Log.Fatal("Config file not found: {Path}", configPath);
        return 2;
    }

    // 2. Load config
    ConfigAchievementsRoot config;
    try
    {
        var configJson = await File.ReadAllTextAsync(configPath, ct);
        config = JsonSerializer.Deserialize<ConfigAchievementsRoot>(configJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Config file deserialized to null");
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Failed to parse config file: {Path}", configPath);
        return 2;
    }

    if (config.Achievements.Count == 0)
    {
        Log.Warning("Config file contains no achievements. Nothing to do.");
        return 0;
    }

    Log.Information("Config loaded: {Count} achievement(s), update={Update}", config.Achievements.Count, update);

    // 3. Build DI container
    var services = new ServiceCollection();
    services.AddHttpClient("appstoreconnect", client =>
    {
        client.BaseAddress = new Uri("https://api.appstoreconnect.apple.com");
    });
    services.AddHttpClient("s3upload"); // Unauthenticated for signed S3 URLs
    services.AddSingleton(new JwtTokenProvider(keyId, issuerId, privateKeyPath));
    services.AddSingleton<AscHttpClient>(sp =>
    {
        var factory = sp.GetRequiredService<IHttpClientFactory>();
        var jwt = sp.GetRequiredService<JwtTokenProvider>();
        return new AscHttpClient(factory.CreateClient("appstoreconnect"), jwt);
    });
    services.AddSingleton<GameCenterService>();
    services.AddSingleton<AchievementsService>();
    services.AddSingleton<LocalizationsService>();
    services.AddSingleton<AssetUploader>();
    services.AddSingleton<RunReport>();

    var sp = services.BuildServiceProvider();

    var gcService = sp.GetRequiredService<GameCenterService>();
    var achService = sp.GetRequiredService<AchievementsService>();
    var locService = sp.GetRequiredService<LocalizationsService>();
    var uploader = sp.GetRequiredService<AssetUploader>();
    var report = sp.GetRequiredService<RunReport>();

    try
    {
        // Phase 0 — Bootstrap
        var detailId = await gcService.GetOrCreateGameCenterDetailAsync(appId, ct);
        var existing = await achService.ListAllAsync(detailId, ct);
        var existingMap = existing.ToDictionary(
            a => a.VendorIdentifier ?? string.Empty,
            a => a,
            StringComparer.OrdinalIgnoreCase);

        // Phase 1/2/3 — Process each achievement
        foreach (var cfg in config.Achievements)
        {
            if (string.IsNullOrWhiteSpace(cfg.VendorIdentifier))
            {
                Log.Warning("Achievement with referenceName={Name} has no vendorIdentifier — skipping", cfg.ReferenceName);
                report.RecordSkipped(cfg.ReferenceName, null);
                continue;
            }

            var found = existingMap.TryGetValue(cfg.VendorIdentifier, out var existing_ach);

            try
            {
                if (found && !update)
                {
                    // Phase 2 — SKIP
                    Log.Warning("Achievement {VendorId} already exists (id={Id}) — skipping (use --update to update)",
                        cfg.VendorIdentifier, existing_ach!.Id);
                    report.RecordSkipped(cfg.VendorIdentifier, existing_ach!.Id);
                }
                else if (found && update)
                {
                    // Phase 3 — UPDATE
                    var achievementId = existing_ach!.Id;
                    await achService.UpdateAsync(achievementId, cfg, ct);
                    await ProcessLocalizationsAsync(locService, uploader, achService,
                        achievementId, cfg, imageFolder, isUpdate: true, ct);
                    report.RecordUpdated(cfg.VendorIdentifier, achievementId);
                }
                else
                {
                    // Phase 1 — CREATE
                    var achievementId = await achService.CreateAsync(detailId, cfg, ct);
                    await ProcessLocalizationsAsync(locService, uploader, achService,
                        achievementId, cfg, imageFolder, isUpdate: false, ct);
                    report.RecordCreated(cfg.VendorIdentifier, achievementId);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to process achievement {VendorId}", cfg.VendorIdentifier);
                report.RecordFailed(cfg.VendorIdentifier, ex.Message, found ? existing_ach?.Id : null);
            }
        }
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Fatal error during run");
        report.PrintSummary();
        await report.WriteJsonReportAsync(ct);
        return 2;
    }

    // Final report
    report.PrintSummary();
    await report.WriteJsonReportAsync(ct);

    return report.HasFailures ? 1 : 0;
}

async Task ProcessLocalizationsAsync(
    LocalizationsService locService,
    AssetUploader uploader,
    AchievementsService achService,
    string achievementId,
    ConfigAchievement cfg,
    string? imageFolder,
    bool isUpdate,
    CancellationToken ct)
{
    Dictionary<string, string>? existingLocMap = null;

    if (isUpdate)
    {
        var existingLocs = await achService.GetLocalizationsAsync(achievementId, ct);
        existingLocMap = existingLocs.ToDictionary(
            l => l.Locale ?? string.Empty,
            l => l.Id,
            StringComparer.OrdinalIgnoreCase);
    }

    foreach (var locale in cfg.Localizations)
    {
        try
        {
            string localizationId;

            if (isUpdate && existingLocMap!.TryGetValue(locale.LocaleCode, out var existingLocId))
            {
                await locService.UpdateAsync(existingLocId, locale, ct);
                localizationId = existingLocId;
            }
            else
            {
                localizationId = await locService.CreateAsync(achievementId, locale, ct);
            }

            // Resolve image path: locale-level override > achievement-level > imageFileName fallback
            var imageFilePath = ResolveImagePath(cfg, locale, imageFolder);
            if (imageFilePath != null)
            {
                if (File.Exists(imageFilePath))
                {
                    try
                    {
                        await uploader.UploadAsync(localizationId, imageFilePath, ct);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Image upload failed for localization {Locale} ({LocId}): {Error}",
                            locale.LocaleCode, localizationId, ex.Message);
                    }
                }
                else
                {
                    Log.Warning("Image file not found: {Path} (locale={Locale})", imageFilePath, locale.LocaleCode);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Skipping locale {Locale}: {Error}", locale.LocaleCode, ex.Message);
        }
    }
}

string? ResolveImagePath(ConfigAchievement cfg, AppStoreConnectCli.Models.ConfigLocale locale, string? imageFolder)
{
    // Priority: per-locale imageFile > achievement imageFile > achievement imageFileName
    var raw = locale.ImageFile ?? cfg.ImageFile ?? cfg.ImageFileName;
    if (raw == null) return null;

    if (imageFolder != null)
        return Path.Combine(imageFolder, Path.GetFileName(raw));

    return raw;
}
