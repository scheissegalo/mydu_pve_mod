using System.Threading.Tasks;

namespace Mod.DynamicEncounters.Features.Common.Interfaces;

/// <summary>Posts wreck info (name, coords, despawn time) to general chat when a player destroys an NPC.</summary>
public interface IWreckChatNotificationService
{
    /// <summary>Posts ship name, coordinates (::pos format), and despawn time to general chat. Sets auto-delete (3h) if not already set.</summary>
    Task NotifyWreckDestroyedAsync(ulong constructId, string? shipName, double posX, double posY, double posZ);
}
