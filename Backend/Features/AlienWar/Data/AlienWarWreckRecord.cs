using System;
using NQ;

namespace Mod.DynamicEncounters.Features.AlienWar.Data;

public class AlienWarWreckRecord
{
    public Guid Id { get; set; }
    public ulong CoreConstructId { get; set; }
    public ulong WreckConstructId { get; set; }
    public string ShipName { get; set; } = string.Empty;
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public double PositionZ { get; set; }
    public DateTime DestroyedAt { get; set; }
}
