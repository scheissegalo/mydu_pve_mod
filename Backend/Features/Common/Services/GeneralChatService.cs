using System;
using System.Threading.Tasks;
using Backend;
using Mod.DynamicEncounters.Features.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mod.DynamicEncounters.Helpers;
using NQ;
using NQ.Interfaces;
using NQutils.Exceptions;

namespace Mod.DynamicEncounters.Features.Common.Services;

public class GeneralChatService(IServiceProvider provider) : IGeneralChatService
{
    private readonly ILogger<GeneralChatService> _logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger<GeneralChatService>();

    public async Task SendToGeneralAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;
        try
        {
            var orleans = provider.GetOrleans();
            var chatGrain = orleans.GetChatGrain(ModBase.Bot.PlayerId);
            await chatGrain.SendMessage(
                new MessageContent
                {
                    message = message.Trim(),
                    channel = new MessageChannel
                    {
                        channel = MessageChannelType.SUPPORT,
                        targetId = 0,
                        channelFilter = ""
                    }
                });
        }
        catch (BusinessException ex)
        {
            _logger.LogWarning(ex, "GeneralChatService: Failed to send to general chat");
        }
    }
}
