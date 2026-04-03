using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mod.DynamicEncounters.Features.Common.Data;
using Newtonsoft.Json;

namespace Mod.DynamicEncounters.Features.Common.Services;

/// <summary>
/// Posts to ModGameAPI broadcast-notification. Base URL: <see cref="BaseUrlEnvVar"/> if set;
/// else if running in a Linux container (<c>/.dockerenv</c>), <c>http://{MOD_GAME_API_DOCKER_HOST}:{MOD_GAME_API_PORT}</c>
/// (defaults <see cref="DefaultDockerHost"/>:<see cref="DefaultPort"/>); else <see cref="DefaultLocalBaseUrl"/>.
/// </summary>
public class BroadcastTopNotificationService(IServiceProvider provider) : IBroadcastTopNotificationService
{
    public const string BaseUrlEnvVar = "MOD_GAME_API_BASE_URL";

    /// <summary>Used when not in Docker and <see cref="BaseUrlEnvVar"/> is unset (process runs on the same host as the API).</summary>
    public const string DefaultLocalBaseUrl = "http://localhost:7780";

    /// <summary>Hostname on the Docker compose network where ModGameAPI listens (override with MOD_GAME_API_DOCKER_HOST).</summary>
    public const string DockerHostEnvVar = "MOD_GAME_API_DOCKER_HOST";

    public const string DefaultDockerHost = "10.5.0.13";

    public const string PortEnvVar = "MOD_GAME_API_PORT";
    public const int DefaultPort = 7780;

    public const string EncounterTitleEnvVar = "ENCOUNTER_ENTER_NOTIFICATION_TITLE";
    public const string EncounterMessageEnvVar = "ENCOUNTER_ENTER_NOTIFICATION_MESSAGE";
    public const string EncounterDurationMsEnvVar = "ENCOUNTER_ENTER_NOTIFICATION_DURATION_MS";
    public const string EncounterVariantEnvVar = "ENCOUNTER_ENTER_NOTIFICATION_VARIANT";
    public const string EncounterBackgroundEnvVar = "ENCOUNTER_ENTER_NOTIFICATION_BACKGROUND_COLOR";
    public const string EncounterTextColorEnvVar = "ENCOUNTER_ENTER_NOTIFICATION_TEXT_COLOR";

    public const string WreckTitleEnvVar = "WRECK_ENTER_NOTIFICATION_TITLE";
    public const string WreckMessageEnvVar = "WRECK_ENTER_NOTIFICATION_MESSAGE";
    public const string WreckDurationMsEnvVar = "WRECK_ENTER_NOTIFICATION_DURATION_MS";
    public const string WreckVariantEnvVar = "WRECK_ENTER_NOTIFICATION_VARIANT";
    public const string WreckBackgroundEnvVar = "WRECK_ENTER_NOTIFICATION_BACKGROUND_COLOR";
    public const string WreckTextColorEnvVar = "WRECK_ENTER_NOTIFICATION_TEXT_COLOR";

    /// <summary>Semi-transparent red (#RRGGBBAA) when hostile bar colors are not overridden.</summary>
    public const string DefaultHostileBarBackground = "#C62828B3";

    /// <summary>Semi-transparent blue (#RRGGBBAA) when wreck bar colors are not overridden.</summary>
    public const string DefaultWreckBarBackground = "#1565C0B3";

    public const string DefaultHostileBarTextColor = "#FFEBEE";
    public const string DefaultWreckBarTextColor = "#E3F2FD";

    public const string KillRewardTitleEnvVar = "KILL_REWARD_NOTIFICATION_TITLE";
    public const string KillRewardDurationMsEnvVar = "KILL_REWARD_NOTIFICATION_DURATION_MS";
    public const string KillRewardVariantEnvVar = "KILL_REWARD_NOTIFICATION_VARIANT";
    public const string KillRewardBackgroundEnvVar = "KILL_REWARD_NOTIFICATION_BACKGROUND_COLOR";
    public const string KillRewardTextColorEnvVar = "KILL_REWARD_NOTIFICATION_TEXT_COLOR";

    /// <summary>Semi-transparent green when kill-reward bar colors are not overridden.</summary>
    public const string DefaultKillRewardBarBackground = "#2E7D32B3";

    public const string DefaultKillRewardBarTextColor = "#E8F5E9";

    public async Task SendTopNotificationAsync(BroadcastTopNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var logger = provider.GetRequiredService<ILogger<BroadcastTopNotificationService>>();

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            logger.LogWarning("BroadcastTopNotification: skipped empty message");
            return;
        }

        var baseUrl = ResolveBaseUrl().TrimEnd('/');
        var url = $"{baseUrl}/api/metamessage/broadcast-notification";

        var payload = new Dictionary<string, object?> { ["message"] = request.Message };

        if (!string.IsNullOrEmpty(request.Title))
        {
            payload["title"] = request.Title;
        }

        if (request.DurationMs.HasValue)
        {
            payload["durationMs"] = request.DurationMs.Value;
        }

        if (!string.IsNullOrEmpty(request.Variant))
        {
            payload["variant"] = request.Variant;
        }

        if (!string.IsNullOrEmpty(request.BackgroundColor))
        {
            payload["backgroundColor"] = request.BackgroundColor;
        }

        if (!string.IsNullOrEmpty(request.TextColor))
        {
            payload["textColor"] = request.TextColor;
        }

        if (request.PlayerIds is { Count: > 0 })
        {
            payload["playerIds"] = request.PlayerIds.ToList();
        }

        var json = JsonConvert.SerializeObject(payload);
        var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();

        try
        {
            using var httpClient = httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Remove("Accept");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await httpClient.PostAsync(url, content, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                logger.LogWarning(
                    "BroadcastTopNotification: HTTP {Status} from {Url}: {Body}",
                    (int)response.StatusCode,
                    url,
                    body);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "BroadcastTopNotification: request to {Url} failed", url);
        }
    }

    public Task SendEncounterZoneEnterBarAsync(IReadOnlyCollection<ulong> playerIds,
        SectorEnterZoneKind zoneKind = SectorEnterZoneKind.Hostile,
        string? sectorDisplayName = null,
        CancellationToken cancellationToken = default)
    {
        if (playerIds == null || playerIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        var name = string.IsNullOrWhiteSpace(sectorDisplayName) ? null : sectorDisplayName.Trim();

        string title;
        string message;
        string? variant;
        string? background;
        string? textColor;
        int? durationMs;

        if (zoneKind == SectorEnterZoneKind.Wreck)
        {
            title = Environment.GetEnvironmentVariable(WreckTitleEnvVar) ?? "Wreck sector";
            message = Environment.GetEnvironmentVariable(WreckMessageEnvVar) ??
                      "You have entered a wreck site — salvage carefully.";
            variant = Environment.GetEnvironmentVariable(WreckVariantEnvVar);
            background = Environment.GetEnvironmentVariable(WreckBackgroundEnvVar);
            textColor = Environment.GetEnvironmentVariable(WreckTextColorEnvVar);
            durationMs = ParsePositiveIntEnv(WreckDurationMsEnvVar);

            if (string.IsNullOrEmpty(background))
            {
                background = DefaultWreckBarBackground;
            }

            if (string.IsNullOrEmpty(textColor))
            {
                textColor = DefaultWreckBarTextColor;
            }
        }
        else
        {
            title = Environment.GetEnvironmentVariable(EncounterTitleEnvVar) ?? "Hostile sector";
            message = Environment.GetEnvironmentVariable(EncounterMessageEnvVar) ??
                      "You have entered a hostile zone — stay alert.";
            variant = Environment.GetEnvironmentVariable(EncounterVariantEnvVar);
            background = Environment.GetEnvironmentVariable(EncounterBackgroundEnvVar);
            textColor = Environment.GetEnvironmentVariable(EncounterTextColorEnvVar);
            durationMs = ParsePositiveIntEnv(EncounterDurationMsEnvVar);

            if (string.IsNullOrEmpty(background))
            {
                background = DefaultHostileBarBackground;
            }

            if (string.IsNullOrEmpty(textColor))
            {
                textColor = DefaultHostileBarTextColor;
            }
        }

        if (name != null)
        {
            title = $"{title} — {name}";
        }

        var request = new BroadcastTopNotificationRequest
        {
            Title = title,
            Message = message,
            DurationMs = durationMs,
            Variant = string.IsNullOrEmpty(variant) ? null : variant,
            BackgroundColor = background,
            TextColor = textColor,
            PlayerIds = playerIds.Distinct().ToList()
        };

        return SendTopNotificationAsync(request, cancellationToken);
    }

    public Task SendKillRewardQuantaNotificationAsync(ulong playerId, long shareRaw, long totalPoolRaw,
        int splitAmongCount, string? titleOverride = null, CancellationToken cancellationToken = default)
    {
        if (splitAmongCount <= 0)
        {
            return Task.CompletedTask;
        }

        var title = !string.IsNullOrWhiteSpace(titleOverride)
            ? titleOverride.Trim()
            : Environment.GetEnvironmentVariable(KillRewardTitleEnvVar) ?? "Kill reward";
        var variant = Environment.GetEnvironmentVariable(KillRewardVariantEnvVar);
        var background = Environment.GetEnvironmentVariable(KillRewardBackgroundEnvVar);
        var textColor = Environment.GetEnvironmentVariable(KillRewardTextColorEnvVar);
        var durationMs = ParsePositiveIntEnv(KillRewardDurationMsEnvVar);

        if (string.IsNullOrEmpty(background))
        {
            background = DefaultKillRewardBarBackground;
        }

        if (string.IsNullOrEmpty(textColor))
        {
            textColor = DefaultKillRewardBarTextColor;
        }

        var shareH = (shareRaw / 100.0).ToString("N2", CultureInfo.InvariantCulture);
        var totalH = (totalPoolRaw / 100.0).ToString("N2", CultureInfo.InvariantCulture);
        var message =
            $"You received {shareH} h. Total pool {totalH} h split among {splitAmongCount} players.";

        var request = new BroadcastTopNotificationRequest
        {
            Title = title,
            Message = message,
            DurationMs = durationMs,
            Variant = string.IsNullOrEmpty(variant) ? null : variant,
            BackgroundColor = background,
            TextColor = textColor,
            PlayerIds = [playerId]
        };

        return SendTopNotificationAsync(request, cancellationToken);
    }

    private static int? ParsePositiveIntEnv(string envVar)
    {
        var raw = Environment.GetEnvironmentVariable(envVar);
        if (string.IsNullOrEmpty(raw) || !int.TryParse(raw, out var d) || d <= 0)
        {
            return null;
        }

        return d;
    }

    /// <summary>
    /// <paramref name="BaseUrlEnvVar"/> wins. Otherwise, inside a Linux Docker container <c>localhost</c> is wrong;
    /// use the game API service on the bridge network (defaults to <see cref="DefaultDockerHost"/>).
    /// </summary>
    internal static string ResolveBaseUrl()
    {
        var explicitUrl = Environment.GetEnvironmentVariable(BaseUrlEnvVar);
        if (!string.IsNullOrWhiteSpace(explicitUrl))
        {
            return explicitUrl.Trim();
        }

        if (File.Exists("/.dockerenv"))
        {
            var host = (Environment.GetEnvironmentVariable(DockerHostEnvVar) ?? DefaultDockerHost).Trim();
            var port = DefaultPort;
            var portRaw = Environment.GetEnvironmentVariable(PortEnvVar);
            if (!string.IsNullOrWhiteSpace(portRaw) && int.TryParse(portRaw, out var p) && p > 0 && p <= 65535)
            {
                port = p;
            }

            return $"http://{host}:{port}";
        }

        return DefaultLocalBaseUrl;
    }
}
