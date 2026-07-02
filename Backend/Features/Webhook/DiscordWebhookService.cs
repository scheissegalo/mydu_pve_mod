using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Mod.DynamicEncounters.Features.Webhook.Interfaces;

namespace Mod.DynamicEncounters.Features.Webhook;

public class DiscordWebhookService(IHttpClientFactory httpClientFactory, ILogger<DiscordWebhookService> logger)
    : IDiscordWebhookService
{
    public void NotifyError(string title, string message, ulong? constructId = null, Exception? exception = null)
    {
        var webhookUrl = Environment.GetEnvironmentVariable("DISCORD_WEBHOOK_URL");
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                var description = message;
                if (constructId.HasValue)
                {
                    description = $"Construct: `{constructId}`\n{description}";
                }

                if (exception != null)
                {
                    description += $"\n\n**{exception.GetType().Name}**: {exception.Message}";
                    if (!string.IsNullOrEmpty(exception.StackTrace))
                    {
                        var trace = exception.StackTrace.Length > 800
                            ? exception.StackTrace[..800] + "…"
                            : exception.StackTrace;
                        description += $"\n```\n{trace}\n```";
                    }
                }

                var payload = new
                {
                    embeds = new[]
                    {
                        new
                        {
                            title,
                            description,
                            color = 0xFF0000,
                            timestamp = DateTime.UtcNow.ToString("o")
                        }
                    }
                };

                var json = JsonSerializer.Serialize(payload);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var client = httpClientFactory.CreateClient();
                using var response = await client.PostAsync(webhookUrl, content);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    logger.LogWarning(
                        "Discord webhook returned {Status}: {Body}",
                        (int)response.StatusCode,
                        body.Length > 200 ? body[..200] : body
                    );
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to post Discord webhook notification");
            }
        });
    }
}
