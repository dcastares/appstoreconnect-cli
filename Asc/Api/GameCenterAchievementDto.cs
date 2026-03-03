using System.Text.Json.Serialization;

namespace AppStoreConnectCli.Asc.Api;

public class GameCenterAchievementAttributes
{
    [JsonPropertyName("referenceName")]
    public string? ReferenceName { get; set; }

    [JsonPropertyName("vendorIdentifier")]
    public string? VendorIdentifier { get; set; }

    [JsonPropertyName("points")]
    public int? Points { get; set; }

    [JsonPropertyName("showBeforeEarned")]
    public bool ShowBeforeEarned { get; set; }

    [JsonPropertyName("repeatable")]
    public bool Repeatable { get; set; }

    [JsonPropertyName("archived")]
    public bool Archived { get; set; }
}

public class GameCenterAchievementCreateRequest
{
    [JsonPropertyName("data")]
    public GameCenterAchievementCreateData Data { get; set; } = new();
}

public class GameCenterAchievementCreateData
{
    [JsonPropertyName("type")]
    public string Type => "gameCenterAchievements";

    [JsonPropertyName("attributes")]
    public GameCenterAchievementCreateAttributes Attributes { get; set; } = new();

    [JsonPropertyName("relationships")]
    public GameCenterAchievementCreateRelationships Relationships { get; set; } = new();
}

public class GameCenterAchievementCreateAttributes
{
    [JsonPropertyName("referenceName")]
    public string ReferenceName { get; set; } = string.Empty;

    [JsonPropertyName("vendorIdentifier")]
    public string VendorIdentifier { get; set; } = string.Empty;

    [JsonPropertyName("points")]
    public int Points { get; set; }

    [JsonPropertyName("showBeforeEarned")]
    public bool ShowBeforeEarned { get; set; }

    [JsonPropertyName("repeatable")]
    public bool Repeatable { get; set; }
}

public class GameCenterAchievementCreateRelationships
{
    [JsonPropertyName("gameCenterDetail")]
    public AscRelationship GameCenterDetail { get; set; } = new();
}

public class GameCenterAchievementUpdateRequest
{
    [JsonPropertyName("data")]
    public GameCenterAchievementUpdateData Data { get; set; } = new();
}

public class GameCenterAchievementUpdateData
{
    [JsonPropertyName("type")]
    public string Type => "gameCenterAchievements";

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("attributes")]
    public GameCenterAchievementUpdateAttributes Attributes { get; set; } = new();
}

public class GameCenterAchievementUpdateAttributes
{
    [JsonPropertyName("referenceName")]
    public string? ReferenceName { get; set; }

    [JsonPropertyName("points")]
    public int? Points { get; set; }

    [JsonPropertyName("showBeforeEarned")]
    public bool? ShowBeforeEarned { get; set; }

    [JsonPropertyName("repeatable")]
    public bool? Repeatable { get; set; }
}
