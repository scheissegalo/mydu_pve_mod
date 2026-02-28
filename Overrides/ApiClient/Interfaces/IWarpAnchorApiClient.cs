using System.Threading.Tasks;
using Mod.DynamicEncounters.Overrides.ApiClient.Services;

namespace Mod.DynamicEncounters.Overrides.ApiClient.Interfaces;

public interface IWarpAnchorApiClient
{
    Task SetWarpEndCooldown(SetWarpEndCooldownRequest request);
    /// <summary>Polls Backend for a pending warp destination refresh script for the player. Returns null if none.</summary>
    Task<string?> GetPendingRefreshScriptAsync(ulong playerId);
}