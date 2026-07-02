using Microsoft.Extensions.DependencyInjection;
using Mod.DynamicEncounters.Features.Webhook.Interfaces;

namespace Mod.DynamicEncounters.Features.Webhook;

public static class WebhookRegistration
{
    public static void RegisterWebhookServices(this IServiceCollection services)
    {
        services.AddSingleton<IDiscordWebhookService, DiscordWebhookService>();
    }
}
