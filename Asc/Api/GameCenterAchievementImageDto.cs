using System.Text.Json.Serialization;

namespace AppStoreConnectCli.Asc.Api;

public class GameCenterAchievementImageAttributes
{
    [JsonPropertyName("fileSize")]
    public long? FileSize { get; set; }

    [JsonPropertyName("fileName")]
    public string? FileName { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("uploadOperations")]
    public List<UploadOperation>? UploadOperations { get; set; }

    [JsonPropertyName("assetDeliveryState")]
    public AssetDeliveryState? AssetDeliveryState { get; set; }
}

public class UploadOperation
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("length")]
    public int Length { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("requestHeaders")]
    public List<HttpHeader>? RequestHeaders { get; set; }
}

public class HttpHeader
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

public class AssetDeliveryState
{
    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("errors")]
    public List<AscError>? Errors { get; set; }
}

public class GameCenterAchievementImageCreateRequest
{
    [JsonPropertyName("data")]
    public GameCenterAchievementImageCreateData Data { get; set; } = new();
}

public class GameCenterAchievementImageCreateData
{
    [JsonPropertyName("type")]
    public string Type => "gameCenterAchievementImages";

    [JsonPropertyName("attributes")]
    public GameCenterAchievementImageCreateAttributes Attributes { get; set; } = new();

    [JsonPropertyName("relationships")]
    public GameCenterAchievementImageRelationships Relationships { get; set; } = new();
}

public class GameCenterAchievementImageCreateAttributes
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }
}

public class GameCenterAchievementImageRelationships
{
    [JsonPropertyName("gameCenterAchievementLocalization")]
    public AscRelationship GameCenterAchievementLocalization { get; set; } = new();
}

public class GameCenterAchievementImageCommitRequest
{
    [JsonPropertyName("data")]
    public GameCenterAchievementImageCommitData Data { get; set; } = new();
}

public class GameCenterAchievementImageCommitData
{
    [JsonPropertyName("type")]
    public string Type => "gameCenterAchievementImages";

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("attributes")]
    public GameCenterAchievementImageCommitAttributes Attributes { get; set; } = new();
}

public class GameCenterAchievementImageCommitAttributes
{
    [JsonPropertyName("uploaded")]
    public bool Uploaded { get; set; }
}
