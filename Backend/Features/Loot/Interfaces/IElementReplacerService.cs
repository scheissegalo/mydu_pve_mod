using System.Collections.Generic;
using System.Threading.Tasks;
using Mod.DynamicEncounters.Features.Loot.Data;

namespace Mod.DynamicEncounters.Features.Loot.Interfaces;

public interface IElementReplacerService
{
    Task ReplaceSingleElementAsync(ulong constructId, string elementTypeName, string withElementTypeName);

    Task<ReplaceElementsResult> ReplaceBatchAsync(
        ulong constructId,
        IReadOnlyList<ElementSwapRequest> swaps);
}
