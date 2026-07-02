using System;
using System.Threading.Tasks;
using BotLib.Generated;
using Microsoft.Extensions.Logging;
using NQ;
using NQutils.Exceptions;

namespace Mod.DynamicEncounters.Common.Helpers;

public static class BotSessionGuard
{
    private const int MaxAttempts = 3;

    public static async Task EnsureBotSessionAsync(ILogger? logger = null)
    {
        if (ConstructBehaviorContextCache.IsBotDisconnected)
        {
            logger?.LogWarning("Bot marked disconnected, reconnecting before operation");
            await ReconnectBotAsync();
            return;
        }

        try
        {
            await ModBase.Bot.Req.InventoryGet();
        }
        catch (BusinessException bex) when (bex.error.code == ErrorCode.InvalidSession)
        {
            logger?.LogWarning("Bot session probe failed (InvalidSession), reconnecting");
            await ReconnectBotAsync();
        }
    }

    public static async Task ExecuteWithSessionRetryAsync(Func<Task> operation, ILogger? logger = null)
    {
        await ExecuteWithSessionRetryAsync(
            async () =>
            {
                await operation();
                return true;
            },
            logger
        );
    }

    public static async Task<T> ExecuteWithSessionRetryAsync<T>(Func<Task<T>> operation, ILogger? logger = null)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await EnsureBotSessionAsync(logger);
                return await operation();
            }
            catch (BusinessException bex) when (bex.error.code == ErrorCode.InvalidSession)
            {
                if (attempt >= MaxAttempts)
                {
                    throw;
                }

                logger?.LogWarning(bex, "InvalidSession on attempt {Attempt}, reconnecting", attempt);
                await ReconnectBotAsync();
            }
        }

        throw new InvalidOperationException("Unreachable");
    }

    private static async Task ReconnectBotAsync()
    {
        ConstructBehaviorContextCache.RaiseBotDisconnected();
        await ModBase.Bot.Reconnect();
        ConstructBehaviorContextCache.RaiseBotReconnected();
    }
}
