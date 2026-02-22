using Newtonsoft.Json;
using NQ;

namespace Mod.DynamicEncounters.Features.AlienWar.Data;

public class AlienWarCheckTaskData
{
    [JsonProperty("coreConstructId")]
    public ulong CoreConstructId { get; set; }

    [JsonProperty("sector")]
    public Vec3 Sector { get; set; }

    [JsonProperty("scriptName")]
    public string ScriptName { get; set; } = string.Empty;

    [JsonProperty("cooldownSecondsOverride")]
    public int? CooldownSecondsOverride { get; set; }
}
