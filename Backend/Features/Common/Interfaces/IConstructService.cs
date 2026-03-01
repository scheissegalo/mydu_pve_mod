using System;
using System.Threading.Tasks;
using Mod.DynamicEncounters.Features.Common.Data;
using NQ;

namespace Mod.DynamicEncounters.Features.Common.Interfaces;

public interface IConstructService
{
    Task<ConstructInfoOutcome> GetConstructInfoAsync(ulong constructId);
    Task<ConstructTransformOutcome> GetConstructTransformFromDbAsync(ulong constructId);
    Task<ConstructTransformOutcome> GetConstructTransformAsync(ulong constructId);
    Task ResetConstructCombatLock(ulong constructId);
    Task SetDynamicWreckAsync(ulong constructId, bool isDynamicWreck);
    Task<Velocities> GetConstructVelocities(ulong constructId);
    Task DeleteAsync(ulong constructId);
    Task SoftDeleteAsync(ulong constructId);
    Task SetAutoDeleteFromNowAsync(ulong constructId, TimeSpan timeSpan);
    /// <summary>Gets the server's GC abandoned-construct delete delay (e.g. 720 hours). Used when setting wreck despawn time.</summary>
    TimeSpan GetGcAbandonedConstructDeleteDelay();
    /// <summary>Gets when the construct will be despawned by GC (abandoned_at + deleteDelayHours). Returns null if abandoned_at is not set.</summary>
    Task<DateTime?> GetConstructDespawnTimeUtcAsync(ulong constructId);
    Task<bool> TryVentShieldsAsync(ulong constructId);
    Task<bool> IsBeingControlled(ulong constructId);
    IConstructService NoCache();
    Task<bool> Exists(ulong constructId);
    Task<bool> ExistsAndNotDeleted(ulong constructId);
    Task ActivateShieldsAsync(ulong constructId);
    Task<bool> IsInSafeZone(ulong constructId);
    Task SendIdentificationNotification(ulong constructId, TargetingConstructData targeting);
    Task SendAttackingNotification(ulong constructId, TargetingConstructData targeting);
    Task RenameConstruct(ulong constructId, string name);
    Task ApplyStasisEffect(ulong constructId, double strength, double duration);
    Task<double> GetConstructTotalMass(ulong constructId);
}