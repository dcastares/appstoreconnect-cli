using AppStoreConnectCli.Asc.Api;
using AppStoreConnectCli.Models;
using Serilog;

namespace AppStoreConnectCli.Asc;

public class AchievementResource
{
    public string Id { get; set; } = string.Empty;
    public string? VendorIdentifier { get; set; }
    public string? ReferenceName { get; set; }
}

public class AchievementsService
{
    private readonly AscHttpClient _client;

    public AchievementsService(AscHttpClient client)
    {
        _client = client;
    }

    public async Task<List<AchievementResource>> ListAllAsync(string gameCenterDetailId, CancellationToken ct = default)
    {
        Log.Debug("Listing all achievements for gameCenterDetail {DetailId}", gameCenterDetailId);

        var raw = await _client.GetAllPagesAsync<AscResourceObject<GameCenterAchievementAttributes>>(
            $"/v1/gameCenterDetails/{gameCenterDetailId}/gameCenterAchievements?limit=200", ct);

        var results = raw.Select(r => new AchievementResource
        {
            Id = r.Id,
            VendorIdentifier = r.Attributes?.VendorIdentifier,
            ReferenceName = r.Attributes?.ReferenceName
        }).ToList();

        Log.Information("Found {Count} existing achievements", results.Count);
        return results;
    }

    public async Task<string> CreateAsync(string gameCenterDetailId, ConfigAchievement cfg, CancellationToken ct = default)
    {
        Log.Debug("Creating achievement {VendorId}", cfg.VendorIdentifier);

        var request = new GameCenterAchievementCreateRequest
        {
            Data = new GameCenterAchievementCreateData
            {
                Attributes = new GameCenterAchievementCreateAttributes
                {
                    ReferenceName = cfg.ReferenceName,
                    VendorIdentifier = cfg.VendorIdentifier,
                    Points = cfg.ResolvedPoints,
                    ShowBeforeEarned = cfg.ShowBeforeEarned,
                    Repeatable = cfg.Repeatable
                },
                Relationships = new GameCenterAchievementCreateRelationships
                {
                    GameCenterDetail = new AscRelationship
                    {
                        Data = new AscRelationshipData { Type = "gameCenterDetails", Id = gameCenterDetailId }
                    }
                }
            }
        };

        var response = await _client.PostAsync<
            GameCenterAchievementCreateRequest,
            AscResponse<AscResourceObject<GameCenterAchievementAttributes>>>(
            "/v1/gameCenterAchievements", request, ct);

        var id = response?.Data?.Id
                 ?? throw new InvalidOperationException($"No ID returned when creating achievement {cfg.VendorIdentifier}");

        Log.Information("Created achievement {VendorId} → {Id}", cfg.VendorIdentifier, id);
        return id;
    }

    public async Task UpdateAsync(string achievementId, ConfigAchievement cfg, CancellationToken ct = default)
    {
        Log.Debug("Updating achievement {Id}", achievementId);

        var request = new GameCenterAchievementUpdateRequest
        {
            Data = new GameCenterAchievementUpdateData
            {
                Id = achievementId,
                Attributes = new GameCenterAchievementUpdateAttributes
                {
                    ReferenceName = cfg.ReferenceName,
                    Points = cfg.ResolvedPoints,
                    ShowBeforeEarned = cfg.ShowBeforeEarned,
                    Repeatable = cfg.Repeatable
                }
            }
        };

        await _client.PatchAsync<
            GameCenterAchievementUpdateRequest,
            AscResponse<AscResourceObject<GameCenterAchievementAttributes>>>(
            $"/v1/gameCenterAchievements/{achievementId}", request, ct);

        Log.Information("Updated achievement {Id} ({VendorId})", achievementId, cfg.VendorIdentifier);
    }

    public async Task<List<LocalizationResource>> GetLocalizationsAsync(string achievementId, CancellationToken ct = default)
    {
        Log.Debug("Fetching localizations for achievement {Id}", achievementId);

        var raw = await _client.GetAllPagesAsync<AscResourceObject<GameCenterAchievementLocalizationAttributes>>(
            $"/v1/gameCenterAchievements/{achievementId}/localizations?limit=200", ct);

        return raw.Select(r => new LocalizationResource
        {
            Id = r.Id,
            Locale = r.Attributes?.Locale
        }).ToList();
    }
}

public class LocalizationResource
{
    public string Id { get; set; } = string.Empty;
    public string? Locale { get; set; }
}
