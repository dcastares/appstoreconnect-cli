using System.Text.Json.Serialization;

namespace AppStoreConnectCli.Asc.Api;

public class AscResponse<T>
{
    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("included")]
    public List<AscResourceObject<object>>? Included { get; set; }

    [JsonPropertyName("errors")]
    public List<AscError>? Errors { get; set; }
}

public class AscListResponse<T>
{
    [JsonPropertyName("data")]
    public List<T> Data { get; set; } = new();

    [JsonPropertyName("links")]
    public AscLinks? Links { get; set; }

    [JsonPropertyName("meta")]
    public AscMeta? Meta { get; set; }

    [JsonPropertyName("errors")]
    public List<AscError>? Errors { get; set; }
}

public class AscResourceObject<T>
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("attributes")]
    public T? Attributes { get; set; }

    [JsonPropertyName("relationships")]
    public Dictionary<string, AscRelationship>? Relationships { get; set; }
}

public class AscRelationship
{
    [JsonPropertyName("data")]
    public AscRelationshipData? Data { get; set; }

    [JsonPropertyName("links")]
    public AscLinks? Links { get; set; }
}

public class AscRelationshipData
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}

public class AscLinks
{
    [JsonPropertyName("self")]
    public string? Self { get; set; }

    [JsonPropertyName("next")]
    public string? Next { get; set; }

    [JsonPropertyName("related")]
    public string? Related { get; set; }
}

public class AscMeta
{
    [JsonPropertyName("paging")]
    public AscPaging? Paging { get; set; }
}

public class AscPaging
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("limit")]
    public int Limit { get; set; }
}

public class AscError
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }
}
