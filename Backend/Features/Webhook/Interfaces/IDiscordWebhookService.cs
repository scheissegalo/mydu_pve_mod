using System;

namespace Mod.DynamicEncounters.Features.Webhook.Interfaces;

public interface IDiscordWebhookService
{
    void NotifyError(string title, string message, ulong? constructId = null, Exception? exception = null);
}
