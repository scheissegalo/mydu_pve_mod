using System.Collections.Generic;
using System.Threading.Tasks;
using Mod.DynamicEncounters.Common.Repository;
using Mod.DynamicEncounters.Features.Scripts.Actions.Data;
using NQ;

namespace Mod.DynamicEncounters.Features.Scripts.Actions.Interfaces;

public interface IConstructHandleRepository : IRepository<ConstructHandleItem>
{
    Task<IEnumerable<ConstructHandleItem>> FindTagInSectorAsync(Vec3 sector, string tag);
    Task<IEnumerable<ConstructHandleItem>> FindAlienWarHandlesInSectorAsync(Vec3 sector, ulong alienWarCoreConstructId);
    Task<IEnumerable<ConstructHandleItem>> FindInSectorAsync(Vec3 sector);
    Task<ConstructHandleItem?> FindByConstructIdAsync(ulong constructId);
    Task<IEnumerable<ConstructHandleItem>> FindActiveHandlesAsync();
    Task<IEnumerable<ulong>> FindAllBuggedPoiConstructsAsync();
    Task DeleteByConstructId(ulong constructId);

    Task RemoveHandleAsync(ulong constructId);

    Task<IEnumerable<PoiExpirationData>> GetPoiConstructExpirationTimeSpansAsync();
    
    Task TagAsDeletedConstructHandledThatAreDeletedConstructs();
    Task<int> GetActiveCount();
    Task CleanupOldDeletedConstructHandles();
    /// <summary>Update the handle's current target construct ID in DB (for status/API when behavior cache is in another process).</summary>
    Task UpdateCurrentTargetConstructIdAsync(ulong constructId, ulong? targetConstructId);
}