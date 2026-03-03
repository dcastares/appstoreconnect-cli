using System.Text.Json.Serialization;

namespace AppStoreConnectCli.Asc.Api;

public class GameCenterAchievementLocalizationAttributes
{
    [JsonPropertyName("locale")]
    public string? Locale { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("beforeEarnedDescription")]
    public string? BeforeEarnedDescription { get; set; }

    [JsonPropertyName("afterEarnedDescription")]
    public string? AfterEarnedDescription { get; set; }
}

public class GameCenterAchievementLocalizationCreateRequest
{
    [JsonPropertyName("data")]
    public GameCenterAchievementLocalizationCreateData Data { get; set; } = new();
}

public class GameCenterAchievementLocalizationCreateData
{
    [JsonPropertyName("type")]
    public string Type => "gameCenterAchievementLocalizations";

    [JsonPropertyName("attributes")]
    public GameCenterAchievementLocalizationCreateAttributes Attributes { get; set; } = new();

    [JsonPropertyName("relationships")]
    public GameCenterAchievementLocalizationRelationships Relationships { get; set; } = new();
}

public class GameCenterAchievementLocalizationCreateAttributes
{
    [JsonPropertyName("locale")]
    public string Locale { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("beforeEarnedDescription")]
    public string BeforeEarnedDescription { get; set; } = string.Empty;

    [JsonPropertyName("afterEarnedDescription")]
    public string AfterEarnedDescription { get; set; } = string.Empty;
}

public class GameCenterAchievementLocalizationRelationships
{
    [JsonPropertyName("gameCenterAchievement")]
    public AscRelationship GameCenterAchievement { get; set; } = new();
}

public class GameCenterAchievementLocalizationUpdateRequest
{
    [JsonPropertyName("data")]
    public GameCenterAchievementLocalizationUpdateData Data { get; set; } = new();
}

public class GameCenterAchievementLocalizationUpdateData
{
    [JsonPropertyName("type")]
    public string Type => "gameCenterAchievementLocalizations";

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("attributes")]
    public GameCenterAchievementLocalizationUpdateAttributes Attributes { get; set; } = new();
}

public class GameCenterAchievementLocalizationUpdateAttributes
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("beforeEarnedDescription")]
    public string? BeforeEarnedDescription { get; set; }

    [JsonPropertyName("afterEarnedDescription")]
    public string? AfterEarnedDescription { get; set; }
}
