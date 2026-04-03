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

    /// <summary>
    /// Resolves distinct personal player owners for the given construct ids.
    /// Organization-owned constructs (no player_id) are excluded from the result; callers may log via <see cref="GetOrganizationOnlyConstructCountAmong"/>.
    /// </summary>
    Task<IReadOnlyList<ulong>> GetDistinctPlayerIdsForConstructs(IEnumerable<ulong> constructIds);

    /// <summary>
    /// Counts constructs in the set that are org-owned (no personal player_id). Used for diagnostics when targeted notifications skip those players.
    /// </summary>
    Task<int> GetOrganizationOnlyConstructCountAmong(IEnumerable<ulong> constructIds);

    Task<IEnumerable<ConstructSpatialHashRepository.ConstructSectorRow>> FindPlayerLiveConstructsOnSectorInstances(
        IEnumerable<Vec3> excludeSectorList
    );
}