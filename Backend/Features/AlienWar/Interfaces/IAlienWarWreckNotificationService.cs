using System.Threading.Tasks;

namespace Mod.DynamicEncounters.Features.AlienWar.Interfaces;

/// <summary>Handles Alien War NPC destruction: records wreck and posts to general chat.</summary>
public interface IAlienWarWreckNotificationService
{
    /// <summary>If the construct is part of an Alien War event, records the wreck and posts ship name + coords to general chat.</summary>
    Task NotifyWreckIfAlienWarAsync(ulong constructId, ulong? alienWarCoreId, string? shipName, double posX, double posY, double posZ);
}
