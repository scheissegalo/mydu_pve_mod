using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mod.DynamicEncounters.Features.Common.Interfaces;
using Mod.DynamicEncounters.Helpers;
using Mod.DynamicEncounters.Vector.Helpers;
using NQ;

namespace Mod.DynamicEncounters.Features.Common.Services;

public class WreckChatNotificationService(IServiceProvider provider) : IWreckChatNotificationService
{
    /// <summary>Fallback when GC config is unavailable (e.g. in catch).</summary>
    private static readonly TimeSpan FallbackWreckLifetime = TimeSpan.FromHours(3);
    /// <summary>Delay before announcing wreck so the killer can loot first.</summary>
    private static readonly TimeSpan AnnouncementDelay = TimeSpan.FromMinutes(15);

    private readonly IConstructService _constructService = provider.GetRequiredService<IConstructService>();
    private readonly ILogger<WreckChatNotificationService> _logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger<WreckChatNotificationService>();

    public async Task NotifyWreckDestroyedAsync(ulong constructId, string? shipName, double posX, double posY, double posZ)
    {
        var name = string.IsNullOrWhiteSpace(shipName) ? $"Ship {constructId}" : shipName.Trim();
        var pos = new Vec3 { x = posX, y = posY, z = posZ };
        var posStr = pos.Vec3ToPosition(0, 4);

        DateTime? despawnAt = null;
        try
        {
            despawnAt = await _constructService.GetConstructDespawnTimeUtcAsync(constructId);
            if (!despawnAt.HasValue)
            {
                var gcLifetime = _constructService.GetGcAbandonedConstructDeleteDelay();
                await _constructService.SetAutoDeleteFromNowAsync(constructId, gcLifetime);
                despawnAt = DateTime.UtcNow.Add(gcLifetime);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WreckChatNotificationService: Failed to get/set despawn time for construct {ConstructId}", constructId);
            despawnAt = DateTime.UtcNow.Add(FallbackWreckLifetime);
        }

        var despawnStr = despawnAt.HasValue
            ? $"Despawns at {despawnAt.Value:yyyy-MM-dd HH:mm} UTC ({(despawnAt.Value - DateTime.UtcNow).TotalHours:F1}h left)"
            : "Despawns ~3h";
        var message = $"Wreck: {name} at {posStr} — {despawnStr}";

        _ = Task.Run(async () =>
        {
            await Task.Delay(AnnouncementDelay);
            try
            {
                var chatService = provider.GetRequiredService<IGeneralChatService>();
                await chatService.SendToGeneralAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WreckChatNotificationService: Failed to send wreck notification to general chat");
            }
        });
    }
}
