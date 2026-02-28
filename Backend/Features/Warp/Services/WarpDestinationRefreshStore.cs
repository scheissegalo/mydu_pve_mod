using System.Collections.Concurrent;
using Mod.DynamicEncounters.Features.Warp.Interfaces;

namespace Mod.DynamicEncounters.Features.Warp.Services;

public class WarpDestinationRefreshStore : IWarpDestinationRefreshStore
{
    private readonly ConcurrentDictionary<ulong, string> _pending = new();

    public void SetPendingScript(ulong playerId, string script)
    {
        _pending[playerId] = script;
    }

    public string? GetAndClearPendingScript(ulong playerId)
    {
        return _pending.TryRemove(playerId, out var script) ? script : null;
    }
}
