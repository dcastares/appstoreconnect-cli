using AppStoreConnectCli.Asc.Api;
using AppStoreConnectCli.Models;
using Serilog;

namespace AppStoreConnectCli.Asc;

public class LocalizationsService
{
    private readonly AscHttpClient _client;

    public LocalizationsService(AscHttpClient client)
    {
        _client = client;
    }

    public async Task<string> CreateAsync(string achievementId, ConfigLocale locale, CancellationToken ct = default)
    {
        Log.Debug("Creating localization {Locale} for achievement {Id}", locale.LocaleCode, achievementId);

        var request = new GameCenterAchievementLocalizationCreateRequest
        {
            Data = new GameCenterAchievementLocalizationCreateData
            {
                Attributes = new GameCenterAchievementLocalizationCreateAttributes
                {
                    Locale = locale.LocaleCode,
                    Name = locale.Name,
                    BeforeEarnedDescription = locale.BeforeEarnedDescription,
                    AfterEarnedDescription = locale.AfterEarnedDescription
                },
                Relationships = new GameCenterAchievementLocalizationRelationships
                {
                    GameCenterAchievement = new AscRelationship
                    {
                        Data = new AscRelationshipData { Type = "gameCenterAchievements", Id = achievementId }
                    }
                }
            }
        };

        var response = await _client.PostAsync<
            GameCenterAchievementLocalizationCreateRequest,
            AscResponse<AscResourceObject<GameCenterAchievementLocalizationAttributes>>>(
            "/v1/gameCenterAchievementLocalizations", request, ct);

        var id = response?.Data?.Id
                 ?? throw new InvalidOperationException($"No ID returned when creating localization {locale.LocaleCode}");

        Log.Information("Created localization {Locale} → {Id}", locale.LocaleCode, id);
        return id;
    }

    public async Task UpdateAsync(string localizationId, ConfigLocale locale, CancellationToken ct = default)
    {
        Log.Debug("Updating localization {Id} ({Locale})", localizationId, locale.LocaleCode);

        var request = new GameCenterAchievementLocalizationUpdateRequest
        {
            Data = new GameCenterAchievementLocalizationUpdateData
            {
                Id = localizationId,
                Attributes = new GameCenterAchievementLocalizationUpdateAttributes
                {
                    Name = locale.Name,
                    BeforeEarnedDescription = locale.BeforeEarnedDescription,
                    AfterEarnedDescription = locale.AfterEarnedDescription
                }
            }
        };

        await _client.PatchAsync<
            GameCenterAchievementLocalizationUpdateRequest,
            AscResponse<AscResourceObject<GameCenterAchievementLocalizationAttributes>>>(
            $"/v1/gameCenterAchievementLocalizations/{localizationId}", request, ct);

        Log.Information("Updated localization {Id} ({Locale})", localizationId, locale.LocaleCode);
    }
}
