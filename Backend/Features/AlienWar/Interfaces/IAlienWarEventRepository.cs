using System.Collections.Generic;
using System.Threading.Tasks;
using Mod.DynamicEncounters.Features.AlienWar.Data;

namespace Mod.DynamicEncounters.Features.AlienWar.Interfaces;

public interface IAlienWarEventRepository
{
    Task AddAsync(AlienWarEventRecord record);
    Task RemoveByCoreAsync(ulong coreConstructId);
    Task<IReadOnlyList<AlienWarEventRecord>> GetActiveAsync();
    Task SetLockdownReinforcementsSpawnedAsync(ulong coreConstructId, bool value);
}
