using System.Text.Json.Serialization;

namespace AppStoreConnectCli.Models;

public class ConfigLocale
{
    [JsonPropertyName("localeCode")]
    public string LocaleCode { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("beforeEarnedDescription")]
    public string BeforeEarnedDescription { get; set; } = string.Empty;

    [JsonPropertyName("afterEarnedDescription")]
    public string AfterEarnedDescription { get; set; } = string.Empty;

    // Optional per-locale image override
    [JsonPropertyName("imageFile")]
    public string? ImageFile { get; set; }
}
