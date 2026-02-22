using Mod.DynamicEncounters.Features.AlienWar.Data;
using NQ;

namespace Mod.DynamicEncounters.Features.AlienWar.Interfaces;

public interface IAlienWarStateService
{
    void SetState(ulong coreConstructId, AlienWarEventState state);
    AlienWarEventState? GetState(ulong coreConstructId);
    AlienWarPhase? GetPhase(ulong coreConstructId);
    void RemoveState(ulong coreConstructId);
}
