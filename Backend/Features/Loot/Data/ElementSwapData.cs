using System.Collections.Generic;

namespace Mod.DynamicEncounters.Features.Loot.Data;

public record ElementSwapRequest(string FromElementType, string ToElementType);

public class ReplaceElementError
{
    public string FromElementType { get; set; } = "";
    public string ToElementType { get; set; } = "";
    public string Message { get; set; } = "";
    public string? LinkWarning { get; set; }
}

public class ReplaceElementsResult
{
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public List<ReplaceElementError> Errors { get; set; } = [];
    public List<ReplaceElementError> Warnings { get; set; } = [];
}

public class ReplaceBatchRequest
{
    public List<ElementSwapRequest> Swaps { get; set; } = [];
}
