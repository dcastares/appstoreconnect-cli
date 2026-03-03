using System.Text.Json.Serialization;

namespace AppStoreConnectCli.Models;

public class ConfigAchievement
{
    [JsonPropertyName("referenceName")]
    public string ReferenceName { get; set; } = string.Empty;

    [JsonPropertyName("vendorIdentifier")]
    public string VendorIdentifier { get; set; } = string.Empty;

    // Accept both "points" and "pointValue"
    [JsonPropertyName("points")]
    public int? Points { get; set; }

    [JsonPropertyName("pointValue")]
    public int? PointValue { get; set; }

    [JsonIgnore]
    public int ResolvedPoints => Points ?? PointValue ?? 0;

    [JsonPropertyName("showBeforeEarned")]
    public bool ShowBeforeEarned { get; set; }

    [JsonPropertyName("repeatable")]
    public bool Repeatable { get; set; }

    [JsonPropertyName("position")]
    public int? Position { get; set; }

    // imageFile path (used directly or combined with --imagefolder)
    [JsonPropertyName("imageFile")]
    public string? ImageFile { get; set; }

    // Fallback image filename
    [JsonPropertyName("imageFileName")]
    public string? ImageFileName { get; set; }

    // Accept both "localizations" and "locale" as the key name
    [JsonPropertyName("localizations")]
    public List<ConfigLocale>? LocalizationsRaw { get; set; }

    [JsonPropertyName("locale")]
    public List<ConfigLocale>? LocaleRaw { get; set; }

    [JsonIgnore]
    public List<ConfigLocale> Localizations => LocalizationsRaw ?? LocaleRaw ?? new();
}
