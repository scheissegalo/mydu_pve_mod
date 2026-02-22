using System;

namespace Mod.DynamicEncounters.Features.AlienWar.Data;

public class AlienCoreShieldStatus
{
    public bool ShieldEnabled { get; set; }
    public DateTime? LockdownExitAtUtc { get; set; }
    /// <summary>Raw value from DB: lockdown end as Unix time in milliseconds (UTC). For debugging.</summary>
    public long? LockdownEndUnixMs { get; set; }
    /// <summary>Shield health 0–100%, from (1 - totalDamage/shieldMaxHp). Null if not available.</summary>
    public double? ShieldHealthPercent { get; set; }

    public bool IsInLockdown =>
        ShieldEnabled &&
        LockdownExitAtUtc.HasValue &&
        DateTime.UtcNow < LockdownExitAtUtc.Value;

    /// <summary>Seconds until lockdown end (positive). Null if no lockdown end or already ended.</summary>
    public double? LockdownEndsInSeconds =>
        LockdownExitAtUtc.HasValue && DateTime.UtcNow < LockdownExitAtUtc.Value
            ? (LockdownExitAtUtc.Value - DateTime.UtcNow).TotalSeconds
            : null;

    /// <summary>Seconds since lockdown end (positive). Null if no lockdown end or not yet ended.</summary>
    public double? LockdownEndedAgoSeconds =>
        LockdownExitAtUtc.HasValue && DateTime.UtcNow >= LockdownExitAtUtc.Value
            ? (DateTime.UtcNow - LockdownExitAtUtc.Value).TotalSeconds
            : null;
}
