using System.Collections.Generic;
using System.Threading.Tasks;
using Mod.DynamicEncounters.Features.AlienWar.Data;

namespace Mod.DynamicEncounters.Features.AlienWar.Interfaces;

public interface IAlienWarWreckRepository
{
    Task AddAsync(AlienWarWreckRecord record);
    Task<IReadOnlyList<AlienWarWreckRecord>> FindByCoreAsync(ulong coreConstructId);
}
