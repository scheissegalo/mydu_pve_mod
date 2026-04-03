using System.Collections.Generic;

namespace Mod.DynamicEncounters.Features.Common.Data;

/// <summary>
/// Payload for ModGameAPI <c>POST /api/metamessage/broadcast-notification</c>.
/// </summary>
public class BroadcastTopNotificationRequest
{
    public string Message { get; set; } = string.Empty;

    public string? Title { get; set; }

    public int? DurationMs { get; set; }

    public string? Variant { get; set; }

    public string? BackgroundColor { get; set; }

    public string? TextColor { get; set; }

    /// <summary>
    /// When non-empty, only these players receive the bar. When null or empty, the API may broadcast to everyone (avoid passing empty for encounter flows).
    /// </summary>
    public IReadOnlyList<ulong>? PlayerIds { get; set; }
}
