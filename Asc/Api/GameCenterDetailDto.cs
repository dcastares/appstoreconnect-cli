using System.Text.Json.Serialization;

namespace AppStoreConnectCli.Asc.Api;

public class GameCenterDetailAttributes
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
}

public class GameCenterDetailCreateRequest
{
    [JsonPropertyName("data")]
    public GameCenterDetailCreateData Data { get; set; } = new();
}

public class GameCenterDetailCreateData
{
    [JsonPropertyName("type")]
    public string Type => "gameCenterDetails";

    [JsonPropertyName("relationships")]
    public GameCenterDetailRelationships Relationships { get; set; } = new();
}

public class GameCenterDetailRelationships
{
    [JsonPropertyName("app")]
    public AscRelationship App { get; set; } = new();
}

// App resource for includes
public class AppAttributes
{
    [JsonPropertyName("bundleId")]
    public string? BundleId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
