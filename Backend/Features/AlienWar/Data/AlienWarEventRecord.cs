using System;
using NQ;

namespace Mod.DynamicEncounters.Features.AlienWar.Data;

public class AlienWarEventRecord
{
    public Guid Id { get; set; }
    public ulong CoreConstructId { get; set; }
    public Vec3 Sector { get; set; }
    public string ScriptName { get; set; } = string.Empty;
    public int? CooldownSecondsOverride { get; set; }
    public DateTime CreatedAt { get; set; }
}
