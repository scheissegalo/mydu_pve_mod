using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mod.DynamicEncounters.Features.AlienWar.Data;
using Mod.DynamicEncounters.Features.AlienWar.Interfaces;

namespace Mod.DynamicEncounters.Features.AlienWar.Services;

public class AlienWarWreckNotificationService(IServiceProvider provider) : IAlienWarWreckNotificationService
{
    private readonly IAlienWarWreckRepository _wreckRepo = provider.GetRequiredService<IAlienWarWreckRepository>();
    private readonly ILogger<AlienWarWreckNotificationService> _logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger<AlienWarWreckNotificationService>();

    public async Task NotifyWreckIfAlienWarAsync(ulong constructId, ulong? alienWarCoreId, string? shipName, double posX, double posY, double posZ)
    {
        if (!alienWarCoreId.HasValue)
            return;

        var name = string.IsNullOrWhiteSpace(shipName) ? $"Ship {constructId}" : shipName.Trim();

        try
        {
            await _wreckRepo.AddAsync(new AlienWarWreckRecord
            {
                Id = Guid.NewGuid(),
                CoreConstructId = alienWarCoreId.Value,
                WreckConstructId = constructId,
                ShipName = name,
                PositionX = posX,
                PositionY = posY,
                PositionZ = posZ,
                DestroyedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AlienWarWreckNotificationService: Failed to record wreck {ConstructId}", constructId);
        }

        // Chat post is done by WreckChatNotificationService for all wrecks (Alien War + encounters)
    }
}
