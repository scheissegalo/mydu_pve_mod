namespace Mod.DynamicEncounters.Features.Warp.Interfaces;

/// <summary>
/// Stores a pending warp destination refresh script per player. The Overrides mod (in-game, with IPub)
/// polls via GET /warp/pending-refresh/{playerId} and injects the script so the client can refresh
/// without relog when running standalone (no IPub in Backend).
/// </summary>
public interface IWarpDestinationRefreshStore
{
    void SetPendingScript(ulong playerId, string script);
    string? GetAndClearPendingScript(ulong playerId);
}
