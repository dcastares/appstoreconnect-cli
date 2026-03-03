using System.Text.Json;
using AppStoreConnectCli.Asc.Api;
using Serilog;

namespace AppStoreConnectCli.Asc;

public class GameCenterService
{
    private readonly AscHttpClient _client;

    public GameCenterService(AscHttpClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Returns the gameCenterDetail ID for the given app, creating one if needed.
    /// </summary>
    public async Task<string> GetOrCreateGameCenterDetailAsync(string appId, CancellationToken ct = default)
    {
        Log.Debug("Fetching gameCenterDetail for app {AppId}", appId);

        var appResponse = await _client.GetAsync<JsonDocument>(
            $"/v1/apps/{appId}?include=gameCenterDetail", ct);

        // Look for gameCenterDetail in included
        var detailId = TryExtractIncludedId(appResponse, "gameCenterDetails");
        if (detailId != null)
        {
            Log.Information("Found existing gameCenterDetail {DetailId}", detailId);
            return detailId;
        }

        // Also check relationships link
        detailId = TryExtractRelationshipId(appResponse, "gameCenterDetail");
        if (detailId != null)
        {
            Log.Information("Found gameCenterDetail relationship {DetailId}", detailId);
            return detailId;
        }

        Log.Information("No gameCenterDetail found for app {AppId}, creating one...", appId);
        return await CreateGameCenterDetailAsync(appId, ct);
    }

    private async Task<string> CreateGameCenterDetailAsync(string appId, CancellationToken ct)
    {
        var request = new GameCenterDetailCreateRequest
        {
            Data = new GameCenterDetailCreateData
            {
                Relationships = new GameCenterDetailRelationships
                {
                    App = new AscRelationship
                    {
                        Data = new AscRelationshipData { Type = "apps", Id = appId }
                    }
                }
            }
        };

        var response = await _client.PostAsync<GameCenterDetailCreateRequest, AscResponse<AscResourceObject<GameCenterDetailAttributes>>>(
            "/v1/gameCenterDetails", request, ct);

        var id = response?.Data?.Id
                 ?? throw new InvalidOperationException("Failed to create gameCenterDetail — no ID returned");

        Log.Information("Created gameCenterDetail {DetailId}", id);
        return id;
    }

    private static string? TryExtractIncludedId(JsonDocument doc, string resourceType)
    {
        if (!doc.RootElement.TryGetProperty("included", out var included))
            return null;

        foreach (var item in included.EnumerateArray())
        {
            if (item.TryGetProperty("type", out var type) &&
                type.GetString() == resourceType &&
                item.TryGetProperty("id", out var id))
            {
                return id.GetString();
            }
        }
        return null;
    }

    private static string? TryExtractRelationshipId(JsonDocument doc, string relationshipName)
    {
        if (!doc.RootElement.TryGetProperty("data", out var data))
            return null;
        if (!data.TryGetProperty("relationships", out var rels))
            return null;
        if (!rels.TryGetProperty(relationshipName, out var rel))
            return null;
        if (!rel.TryGetProperty("data", out var relData))
            return null;
        if (relData.ValueKind == JsonValueKind.Null)
            return null;
        if (relData.TryGetProperty("id", out var id))
            return id.GetString();
        return null;
    }
}
