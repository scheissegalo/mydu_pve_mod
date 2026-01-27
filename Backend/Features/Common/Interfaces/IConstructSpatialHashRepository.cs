using System.Collections.Generic;
using System.Threading.Tasks;
using Mod.DynamicEncounters.Features.Common.Repository;
using NQ;

namespace Mod.DynamicEncounters.Features.Common.Interfaces;

public interface IConstructSpatialHashRepository
{
    Task<IEnumerable<ulong>> FindPlayerLiveConstructsOnSector(Vec3 sector);
    Task<long> FindPlayerLiveConstructsCountOnSector(Vec3 sector);
    
    /// <summary>
    /// Find player constructs near a position using spatial query (PostGIS)
    /// This is more accurate than grid-snap lookup when sectors use adaptive grid snap
    /// </summary>
    Task<IEnumerable<ulong>> FindPlayerLiveConstructsNearPosition(Vec3 position, double distance);

    Task<IEnumerable<ConstructSpatialHashRepository.ConstructSectorRow>> FindPlayerLiveConstructsOnSectorInstances(
        IEnumerable<Vec3> excludeSectorList
    );
}