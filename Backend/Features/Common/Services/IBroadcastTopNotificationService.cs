using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mod.DynamicEncounters.Features.Common.Data;

namespace Mod.DynamicEncounters.Features.Common.Services;

public interface IBroadcastTopNotificationService
{
    /// <summary>
    /// Sends a top notification bar via ModGameAPI. Failures are logged only; callers should not rely on delivery.
    /// </summary>
    Task SendTopNotificationAsync(BroadcastTopNotificationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Targeted bar when a player enters an encounter sector. Hostile vs wreck styling and copy use <paramref name="zoneKind"/> and optional <paramref name="sectorDisplayName"/> (e.g. sector instance name).
    /// Environment variables override defaults; see <c>BroadcastTopNotificationService</c> constants.
    /// No-op when <paramref name="playerIds"/> is null or empty.
    /// </summary>
    Task SendEncounterZoneEnterBarAsync(
        IReadOnlyCollection<ulong> playerIds,
        SectorEnterZoneKind zoneKind = SectorEnterZoneKind.Hostile,
        string? sectorDisplayName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Top bar when quanta is granted via <c>give-quanta</c>: your share, total pool, and participant count.
    /// Styling via env vars on <see cref="BroadcastTopNotificationService"/> (KILL_REWARD_* constants).
    /// </summary>
    /// <param name="titleOverride">When set, used as the bar title instead of env/default (e.g. wallet reason).</param>
    Task SendKillRewardQuantaNotificationAsync(
        ulong playerId,
        long shareRaw,
        long totalPoolRaw,
        int splitAmongCount,
        string? titleOverride = null,
        CancellationToken cancellationToken = default);
}
