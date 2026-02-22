using System.Threading.Tasks;
using Mod.DynamicEncounters.Features.AlienWar.Data;

namespace Mod.DynamicEncounters.Features.AlienWar.Interfaces;

public interface IAlienCoreShieldService
{
    Task<AlienCoreShieldStatus?> GetShieldStatusAsync(ulong constructId, int? cooldownSecondsOverride = null);
}
