using System.Collections.Concurrent;
using Mod.DynamicEncounters.Features.AlienWar.Data;
using Mod.DynamicEncounters.Features.AlienWar.Interfaces;

namespace Mod.DynamicEncounters.Features.AlienWar.Services;

public class AlienWarStateService : IAlienWarStateService
{
    private readonly ConcurrentDictionary<ulong, AlienWarEventState> _stateByCore = new();

    public void SetState(ulong coreConstructId, AlienWarEventState state)
    {
        _stateByCore[coreConstructId] = state;
    }

    public AlienWarEventState? GetState(ulong coreConstructId)
    {
        return _stateByCore.TryGetValue(coreConstructId, out var state) ? state : null;
    }

    public AlienWarPhase? GetPhase(ulong coreConstructId)
    {
        return GetState(coreConstructId)?.Phase;
    }

    public void RemoveState(ulong coreConstructId)
    {
        _stateByCore.TryRemove(coreConstructId, out _);
    }
}
