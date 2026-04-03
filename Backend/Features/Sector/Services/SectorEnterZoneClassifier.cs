using System;
using System.Linq;
using Mod.DynamicEncounters.Features.Common.Data;
using Mod.DynamicEncounters.Features.Sector.Data;

namespace Mod.DynamicEncounters.Features.Sector.Services;

/// <summary>
/// Decides whether an activating sector instance should be presented as a wreck site (blue bar) or hostile encounter (red bar).
/// Add tag <see cref="WreckTag"/> on <see cref="SectorInstanceProperties.Tags"/> for explicit classification; otherwise name / on-load script heuristics apply.
/// </summary>
public static class SectorEnterZoneClassifier
{
    /// <summary>Use on sector instance JSON properties tags for explicit wreck styling.</summary>
    public const string WreckTag = "wreck";

    public static SectorEnterZoneKind Classify(SectorInstance sector)
    {
        if (sector == null)
        {
            return SectorEnterZoneKind.Hostile;
        }

        var tags = sector.Properties?.Tags;
        if (tags != null &&
            tags.Any(t => string.Equals(t?.Trim(), WreckTag, StringComparison.OrdinalIgnoreCase)))
        {
            return SectorEnterZoneKind.Wreck;
        }

        if (!string.IsNullOrEmpty(sector.Name) &&
            sector.Name.Contains("wreck", StringComparison.OrdinalIgnoreCase))
        {
            return SectorEnterZoneKind.Wreck;
        }

        if (ScriptTextMentionsWreckPrefab(sector.OnLoadScript))
        {
            return SectorEnterZoneKind.Wreck;
        }

        return SectorEnterZoneKind.Hostile;
    }

    private static bool ScriptTextMentionsWreckPrefab(string? scriptJson)
    {
        if (string.IsNullOrWhiteSpace(scriptJson))
        {
            return false;
        }

        return scriptJson.IndexOf("wreck", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
