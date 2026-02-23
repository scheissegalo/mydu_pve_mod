using System;
using NQ;

namespace Mod.DynamicEncounters.Features.AlienWar.Data;

public enum AlienWarPhase
{
    Attack,
    Guard,
    PostClaim,
    Ended
}

public class AlienWarEventState
{
    public ulong CoreConstructId { get; set; }
    public Vec3 Sector { get; set; }
    public string ScriptName { get; set; } = string.Empty;
    public AlienWarPhase Phase { get; set; }
    public DateTime? LockdownEndAtUtc { get; set; }
    /// <summary>When core was claimed and repaired; used for PostClaim 10-min guard.</summary>
    public DateTime? ClaimedAtUtc { get; set; }
}
