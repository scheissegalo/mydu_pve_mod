using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BotLib.Generated;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mod.DynamicEncounters.Common.Helpers;
using Mod.DynamicEncounters.Features.Loot.Data;
using Mod.DynamicEncounters.Features.Loot.Interfaces;
using Mod.DynamicEncounters.Features.Webhook.Interfaces;
using Mod.DynamicEncounters.Helpers;
using NQ;
using NQ.Interfaces;
using NQutils.Def;
using NQutils.Exceptions;
using Orleans;

namespace Mod.DynamicEncounters.Features.Loot.Service;

public class ElementReplacerService(IServiceProvider provider) : IElementReplacerService
{
    private readonly IClusterClient _orleans = provider.GetOrleans();
    private readonly ILogger<ElementReplacerService> _logger = provider.CreateLogger<ElementReplacerService>();
    private readonly IDiscordWebhookService _discord = provider.GetRequiredService<IDiscordWebhookService>();

    public async Task ReplaceSingleElementAsync(ulong constructId, string elementTypeName, string withElementTypeName)
    {
        var result = await ReplaceBatchAsync(
            constructId,
            [new ElementSwapRequest(elementTypeName, withElementTypeName)]
        );

        if (result.Failed > 0)
        {
            var error = result.Errors.FirstOrDefault()
                ?? new ReplaceElementError { Message = "Element replacement failed" };
            throw new ElementReplacementException(error.Message, error);
        }
    }

    public async Task<ReplaceElementsResult> ReplaceBatchAsync(
        ulong constructId,
        IReadOnlyList<ElementSwapRequest> swaps)
    {
        var result = new ReplaceElementsResult();
        if (swaps.Count == 0)
        {
            return result;
        }

        var replacedElementIds = new HashSet<ulong>();

        foreach (var swap in swaps)
        {
            try
            {
                var linkWarning = await ReplaceSingleElementCoreAsync(
                    constructId,
                    swap.FromElementType,
                    swap.ToElementType,
                    replacedElementIds
                );
                result.Succeeded++;
                if (linkWarning != null)
                {
                    result.Warnings.Add(new ReplaceElementError
                    {
                        FromElementType = swap.FromElementType,
                        ToElementType = swap.ToElementType,
                        Message = linkWarning,
                        LinkWarning = linkWarning
                    });
                }
            }
            catch (Exception ex)
            {
                result.Failed++;
                var error = new ReplaceElementError
                {
                    FromElementType = swap.FromElementType,
                    ToElementType = swap.ToElementType,
                    Message = ex.Message
                };
                result.Errors.Add(error);
                _logger.LogError(
                    ex,
                    "Failed to replace {From} with {To} on construct {ConstructId}",
                    swap.FromElementType,
                    swap.ToElementType,
                    constructId
                );
                _discord.NotifyError(
                    "Element swap failed",
                    $"{swap.FromElementType} → {swap.ToElementType}: {ex.Message}",
                    constructId,
                    ex
                );
            }
        }

        return result;
    }

    /// <returns>Link migration warning if links could not be restored, otherwise null.</returns>
    private async Task<string?> ReplaceSingleElementCoreAsync(
        ulong constructId,
        string elementTypeName,
        string withElementTypeName,
        HashSet<ulong> replacedElementIds)
    {
        var bank = provider.GetGameplayBank();
        var elementDef = bank.GetDefinition(elementTypeName)
            ?? throw new ElementReplacementException($"Unknown element type: {elementTypeName}");

        var replaceElDef = bank.GetDefinition(withElementTypeName)
            ?? throw new ElementReplacementException($"Unknown replacement element type: {withElementTypeName}");

        var constructElementsGrain = _orleans.GetConstructElementsGrain(constructId);
        var elementIds = await constructElementsGrain.GetElementsOfType<ConstructElement>();

        if (elementIds.Count == 0)
        {
            throw new ElementReplacementException($"No elements found on construct {constructId}");
        }

        var candidates = await Task.WhenAll(
            elementIds
                .Where(id => !replacedElementIds.Contains(id))
                .Select(constructElementsGrain.GetElement)
        );

        var element = candidates.FirstOrDefault(x =>
            x.elementType == elementDef.Id || bank.GetDefinition(x.elementType)!.IsChildOf(elementDef.Id));

        if (element == null)
        {
            throw new ElementReplacementException(
                $"No matching element of type {elementTypeName} found on construct {constructId}"
            );
        }

        replacedElementIds.Add(element.elementId);

        var elementInfo = await constructElementsGrain.GetElement(element.elementId);
        var oldLinks = elementInfo.links?.ToList() ?? [];
        var elPos = elementInfo.position;
        var elRot = elementInfo.rotation;
        var oldElementId = element.elementId;

        await BotSessionGuard.ExecuteWithSessionRetryAsync(
            () => ModBase.Bot.Req.BotGiveItems(
                new ItemAndQuantityList
                {
                    content =
                    [
                        new ItemAndQuantity
                        {
                            item = new ItemInfo { type = replaceElDef.Id },
                            quantity = 1
                        }
                    ]
                }
            ),
            _logger
        );

        _logger.LogInformation("Added replacement item to bot inventory");

        var inventory = await BotSessionGuard.ExecuteWithSessionRetryAsync(
            () => ModBase.Bot.Req.InventoryGet(),
            _logger
        );

        var item = inventory.content.FirstOrDefault(x => x.content.type == replaceElDef.Id)
            ?? throw new ElementReplacementException(
                $"Replacement item {withElementTypeName} not found in bot inventory after BotGiveItems"
            );

        var playerId = await GetPlayerWithRightsOnConstruct(constructId);
        var elementManagementGrain = _orleans.GetElementManagementGrain();

        _logger.LogInformation("Construct owner player id = {Id}", playerId);

        var newElement = await BotSessionGuard.ExecuteWithSessionRetryAsync(
            () => ModBase.Bot.Req.ElementAdd(
                new ElementDeploy
                {
                    element = new ElementInfo
                    {
                        constructId = constructId,
                        elementType = replaceElDef.Id,
                        position = elPos,
                        rotation = elRot
                    },
                    fromInventory = new ItemId
                    {
                        ownerId = item.content.owner,
                        instanceId = item.content.id,
                        typeId = item.content.type
                    }
                }
            ),
            _logger
        );

        _logger.LogInformation("Deployed replacement element {NewElementId}", newElement.elementId);

        var elInConstruct = new ElementInConstruct
        {
            constructId = constructId,
            elementId = oldElementId
        };

        await elementManagementGrain.ElementDestroy(playerId, elInConstruct);

        _logger.LogInformation("Destroyed replaced element {OldElementId}", oldElementId);

        if (oldLinks.Count == 0)
        {
            return null;
        }

        try
        {
            await MigrateLinksAsync(constructId, oldElementId, newElement.elementId, oldLinks);
            _logger.LogInformation(
                "Migrated {Count} links from element {OldId} to {NewId}",
                oldLinks.Count,
                oldElementId,
                newElement.elementId
            );
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Element swapped but failed to migrate links from {OldId} to {NewId}",
                oldElementId,
                newElement.elementId
            );
            return $"Element replaced but link migration failed: {ex.Message}";
        }
    }

    private async Task MigrateLinksAsync(
        ulong constructId,
        ulong oldElementId,
        ulong newElementId,
        List<LinkInfo> oldLinks)
    {
        var remapped = oldLinks.Select(link => new LinkInfo
        {
            constructId = constructId,
            fromElementId = link.fromElementId == oldElementId ? newElementId : link.fromElementId,
            fromPlug = link.fromPlug,
            toElementId = link.toElementId == oldElementId ? newElementId : link.toElementId,
            toPlug = link.toPlug,
            plugType = link.plugType
        }).ToList();

        await BotSessionGuard.ExecuteWithSessionRetryAsync(
            () => ModBase.Bot.Req.ElementLinkBatchEdit(
                new LinkBatchEdit
                {
                    constructId = constructId,
                    toCreate = remapped
                }
            ),
            _logger
        );
    }

    private async Task<ulong> GetPlayerWithRightsOnConstruct(ulong constructId)
    {
        var constructInfoGrain = _orleans.GetConstructInfoGrain(constructId);
        var constructInfo = await constructInfoGrain.Get();
        var ownerId = constructInfo.mutableData.ownerId;
        var playerId = ownerId.playerId;
        if (ownerId.IsOrg())
        {
            playerId = await _orleans.GetOrganizationGrain(ownerId.organizationId)
                .EffectiveSuperLegate();
        }

        return playerId;
    }
}

public class ElementReplacementException : Exception
{
    public ReplaceElementError? ErrorDetail { get; }

    public ElementReplacementException(string message, ReplaceElementError? errorDetail = null) : base(message)
    {
        ErrorDetail = errorDetail;
    }
}
