using NQ;

namespace Mod.DynamicEncounters.Features.Warp.Data;

public class SpawnWarpAnchorCommand
{
    public PlayerId PlayerId { get; set; }
    public required Vec3 FromPosition { get; set; }
    public required Vec3 TargetPosition { get; set; }
    public required string ElementTypeName { get; set; } = "";
    /// <summary>If true (default), set gameplayTag to "public_warp_beacon". If false, set to empty (private).</summary>
    public bool Public { get; set; } = true;
    /// <summary>Minutes after spawn when the beacon is despawned. Default 2.</summary>
    public double DespawnMinutes { get; set; } = 2;
    /// <summary>Optional custom construct name. If null or empty, uses "[!] &lt;playerName&gt; Warp".</summary>
    public string? Name { get; set; }
}