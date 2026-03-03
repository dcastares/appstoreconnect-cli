using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace AppStoreConnectCli.Reporting;

public enum ResultStatus
{
    Created,
    Updated,
    Skipped,
    Failed
}

public class AchievementResult
{
    public string VendorIdentifier { get; set; } = string.Empty;
    public string? AscId { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ResultStatus Status { get; set; }

    public string? Error { get; set; }
}

public class RunReport
{
    private readonly List<AchievementResult> _results = new();

    public void Record(AchievementResult result)
    {
        _results.Add(result);
    }

    public void RecordCreated(string vendorId, string ascId)
        => Record(new AchievementResult { VendorIdentifier = vendorId, AscId = ascId, Status = ResultStatus.Created });

    public void RecordUpdated(string vendorId, string ascId)
        => Record(new AchievementResult { VendorIdentifier = vendorId, AscId = ascId, Status = ResultStatus.Updated });

    public void RecordSkipped(string vendorId, string? ascId = null)
        => Record(new AchievementResult { VendorIdentifier = vendorId, AscId = ascId, Status = ResultStatus.Skipped });

    public void RecordFailed(string vendorId, string error, string? ascId = null)
        => Record(new AchievementResult { VendorIdentifier = vendorId, AscId = ascId, Status = ResultStatus.Failed, Error = error });

    public bool HasFailures => _results.Any(r => r.Status == ResultStatus.Failed);

    public void PrintSummary()
    {
        var created = _results.Count(r => r.Status == ResultStatus.Created);
        var updated = _results.Count(r => r.Status == ResultStatus.Updated);
        var skipped = _results.Count(r => r.Status == ResultStatus.Skipped);
        var failed = _results.Count(r => r.Status == ResultStatus.Failed);

        Log.Information("=== Run Summary ===");
        Log.Information("  Created : {Count}", created);
        Log.Information("  Updated : {Count}", updated);
        Log.Information("  Skipped : {Count}", skipped);
        Log.Information("  Failed  : {Count}", failed);

        if (failed > 0)
        {
            Log.Warning("Failed achievements:");
            foreach (var r in _results.Where(r => r.Status == ResultStatus.Failed))
                Log.Warning("  {VendorId}: {Error}", r.VendorIdentifier, r.Error);
        }
    }

    public async Task WriteJsonReportAsync(CancellationToken ct = default)
    {
        var outDir = Path.Combine(Directory.GetCurrentDirectory(), "out");
        Directory.CreateDirectory(outDir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var reportPath = Path.Combine(outDir, $"report_{timestamp}.json");

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(_results, options);
        await File.WriteAllTextAsync(reportPath, json, ct);

        Log.Information("Report written to {Path}", reportPath);
    }
}
