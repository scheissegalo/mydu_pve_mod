using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Backend;
using Mod.DynamicEncounters.Features.AlienWar.Data;
using Mod.DynamicEncounters.Features.AlienWar.Interfaces;
using Mod.DynamicEncounters.Features.Common.Interfaces;
using Mod.DynamicEncounters.Features.Scripts.Actions.Data;
using Mod.DynamicEncounters.Features.Scripts.Actions.Interfaces;
using Mod.DynamicEncounters.Features.TaskQueue.Interfaces;
using Mod.DynamicEncounters.Features.VoxelService.Interfaces;
using Mod.DynamicEncounters.Helpers;
using Mod.DynamicEncounters;
using Newtonsoft.Json.Linq;
using NQ;
using NQ.Interfaces;
using NQutils.Def;

namespace Mod.DynamicEncounters.Api.Controllers;

[Route("alienwar")]
public class AlienWarController : Controller
{
    [HttpPost]
    [Route("start")]
    public async Task<IActionResult> Start([FromBody] StartAlienWarRequest request)
    {
        var provider = ModBase.ServiceProvider;
        var constructService = provider.GetRequiredService<IConstructService>();
        var scriptService = provider.GetRequiredService<IScriptService>();
        var stateService = provider.GetRequiredService<IAlienWarStateService>();
        var taskQueueService = provider.GetRequiredService<ITaskQueueService>();

        if (!await constructService.ExistsAndNotDeleted(request.ConstructId))
            return NotFound(new { error = "Construct not found or deleted", constructId = request.ConstructId });

        var voxelService = provider.GetRequiredService<IVoxelServiceClient>();
        await voxelService.TriggerConstructCacheAsync(new ConstructId { constructId = request.ConstructId });

        var properties = new ConcurrentDictionary<string, object>
        {
            ["AlienWarTargetConstructId"] = request.ConstructId
        };

        var context = new ScriptContext(
            provider,
            1,
            [],
            request.Sector,
            null)
        {
            ConstructId = request.ConstructId,
            Properties = properties
        };

        var result = await scriptService.ExecuteScriptAsync(request.ScriptName, context);
        if (!result.Success)
            return BadRequest(new { error = "Script execution failed", message = result.Message });

        stateService.SetState(request.ConstructId, new AlienWarEventState
        {
            CoreConstructId = request.ConstructId,
            Sector = request.Sector,
            ScriptName = request.ScriptName,
            Phase = AlienWarPhase.Attack,
            LockdownEndAtUtc = null
        });

        var eventRepository = provider.GetRequiredService<IAlienWarEventRepository>();
        await eventRepository.AddAsync(new AlienWarEventRecord
        {
            Id = Guid.NewGuid(),
            CoreConstructId = request.ConstructId,
            Sector = request.Sector,
            ScriptName = request.ScriptName,
            CooldownSecondsOverride = request.CooldownSecondsOverride,
            CreatedAt = DateTime.UtcNow,
            LockdownReinforcementsSpawned = false
        });

        await taskQueueService.EnqueueAlienWarCheck(
            new AlienWarCheckTaskData
            {
                CoreConstructId = request.ConstructId,
                Sector = request.Sector,
                ScriptName = request.ScriptName,
                CooldownSecondsOverride = request.CooldownSecondsOverride
            },
            System.DateTime.UtcNow);

        return Ok(new { eventStarted = true, coreConstructId = request.ConstructId });
    }

    /// <summary>Overall Alien War status: all active events with core construct, sector, phase, and bot count per event.</summary>
    [HttpGet]
    [Route("overview")]
    public async Task<IActionResult> GetOverview()
    {
        var provider = ModBase.ServiceProvider;
        var eventRepo = provider.GetRequiredService<IAlienWarEventRepository>();
        var stateService = provider.GetRequiredService<IAlienWarStateService>();
        var constructHandleRepo = provider.GetRequiredService<IConstructHandleRepository>();
        var constructService = provider.GetRequiredService<IConstructService>();

        var active = await eventRepo.GetActiveAsync();
        var eventsList = new List<object>();

        foreach (var evt in active)
        {
            var state = stateService.GetState(evt.CoreConstructId);
            var handles = (await constructHandleRepo.FindAlienWarHandlesInSectorAsync(evt.Sector, evt.CoreConstructId)).ToList();
            var aliveCount = 0;
            foreach (var h in handles)
            {
                if (await constructService.ExistsAndNotDeleted(h.ConstructId))
                    aliveCount++;
            }

            eventsList.Add(new
            {
                coreConstructId = evt.CoreConstructId,
                sector = evt.Sector,
                scriptName = evt.ScriptName,
                phase = state?.Phase.ToString() ?? "Unknown",
                botCount = aliveCount,
                createdAt = evt.CreatedAt
            });
        }

        return Ok(new
        {
            activeEventCount = active.Count,
            events = eventsList
        });
    }

    [HttpGet]
    [Route("status/{constructId:long}")]
    public async Task<IActionResult> GetStatus(ulong constructId)
    {
        var provider = ModBase.ServiceProvider;
        var stateService = provider.GetRequiredService<IAlienWarStateService>();
        var state = stateService.GetState(constructId);
        if (state == null)
            return NotFound(new { error = "No active Alien War event for this construct", constructId });

        // Live shield status (same data the check task uses) for debugging
        int? cooldownOverride = null;
        var eventRepo = provider.GetRequiredService<IAlienWarEventRepository>();
        var active = await eventRepo.GetActiveAsync();
        var evt = active.FirstOrDefault(e => e.CoreConstructId == constructId);
        if (evt != null)
            cooldownOverride = evt.CooldownSecondsOverride;

        var shieldService = provider.GetRequiredService<IAlienCoreShieldService>();
        var shieldStatus = await shieldService.GetShieldStatusAsync(constructId, cooldownOverride);

        // Bot targeting: in Attack phase 1 bot targets core, others players; in Guard all target players
        int aliveBotCount = 0;
        int targetingCore = 0;
        int targetingPlayers = 0;
        var targetingPlayerConstructIds = new List<ulong>();
        if (evt != null)
        {
            var constructHandleRepo = provider.GetRequiredService<IConstructHandleRepository>();
            var constructService = provider.GetRequiredService<IConstructService>();
            var handles = (await constructHandleRepo.FindAlienWarHandlesInSectorAsync(evt.Sector, constructId)).ToList();
            var targetedPlayerConstructIdsSet = new HashSet<ulong>();
            foreach (var h in handles)
            {
                if (!await constructService.ExistsAndNotDeleted(h.ConstructId))
                    continue;
                aliveBotCount++;
                ulong? targetId = null;
                if (ConstructBehaviorContextCache.Data.TryGetValue(h.ConstructId, out var context))
                    targetId = context.GetTargetConstructId();
                if (!targetId.HasValue && h.JsonProperties?.Context != null && h.JsonProperties.Context.TryGetValue("CurrentTargetConstructId", out var ctxVal) && ctxVal != null)
                    targetId = ParseTargetConstructIdFromContext(ctxVal);
                if (targetId.HasValue)
                {
                    if (targetId.Value == constructId)
                        targetingCore++;
                    else
                    {
                        targetingPlayers++;
                        targetedPlayerConstructIdsSet.Add(targetId.Value);
                    }
                }
            }
            targetingPlayerConstructIds = targetedPlayerConstructIdsSet.OrderBy(id => id).ToList();
        }

        // Counts are from actual target data only (cache or handle Context). When we have no target
        // data (e.g. API in different process, or persist not run yet), counts stay 0; when there
        // are no player constructs in range, the non-core bots have no target so targetingPlayers is 0.

        return Ok(new
        {
            phase = state.Phase.ToString(),
            lockdownEndAtUtc = state.LockdownEndAtUtc,
            bots = new { aliveCount = aliveBotCount, targetingCore, targetingPlayers, targetingPlayerConstructIds },
            shield = shieldStatus == null
                ? (object)new { error = "Could not read shield status (construct missing or DB error)" }
                : new
                {
                    shieldEnabled = shieldStatus.ShieldEnabled,
                    lockdownExitAtUtc = shieldStatus.LockdownExitAtUtc,
                    lockdownEndUnixMs = shieldStatus.LockdownEndUnixMs,
                    isInLockdown = shieldStatus.IsInLockdown,
                    immunityEndAtUtc = shieldStatus.ImmunityEndAtUtc,
                    isInImmunity = shieldStatus.IsInImmunity,
                    lockdownEndsInSeconds = shieldStatus.LockdownEndsInSeconds,
                    lockdownEndedAgoSeconds = shieldStatus.LockdownEndedAgoSeconds,
                    shieldHealthPercent = shieldStatus.ShieldHealthPercent
                }
        });
    }

    /// <summary>Cancel an active Alien War event: despawn all bots and stop processing. Useful for testing.</summary>
    [HttpPost]
    [Route("cancel/{constructId:long}")]
    public async Task<IActionResult> Cancel(ulong constructId)
    {
        var provider = ModBase.ServiceProvider;
        var taskQueueService = provider.GetRequiredService<ITaskQueueService>();
        var cancelled = await taskQueueService.CancelAlienWarEventAsync(constructId);
        if (!cancelled)
            return NotFound(new { error = "No active Alien War event for this construct", constructId });
        return Ok(new { cancelled = true, coreConstructId = constructId });
    }

    /// <summary>Set the construct's owner (claim). Optional body: { "playerId": 123 }. If omitted, uses the bot player.</summary>
    [HttpPost]
    [Route("claim/{constructId:long}")]
    public async Task<IActionResult> Claim(ulong constructId, [FromBody] ClaimConstructRequest? body = null)
    {
        var provider = ModBase.ServiceProvider;
        var constructService = provider.GetRequiredService<IConstructService>();
        if (!await constructService.ExistsAndNotDeleted(constructId))
            return NotFound(new { error = "Construct not found or deleted", constructId });

        var playerId = body?.PlayerId ?? ModBase.Bot.PlayerId;
        var orleans = provider.GetOrleans();
        var constructGrain = orleans.GetConstructGrain(constructId);
        // Pass new owner as first arg (like AkimboAdminMod takeover); passing 0 can be treated as disown/destroy.
        await constructGrain.ConstructSetOwner(playerId, new ConstructOwnerSet { ownerId = new EntityId { playerId = playerId } }, doKeyCheck: false);

        return Ok(new { claimed = true, constructId, ownerPlayerId = playerId });
    }

    /// <summary>Repair all elements on the construct (set hitpointsRatio = 1.0 for each element).</summary>
    [HttpPost]
    [Route("repair/{constructId:long}")]
    public async Task<IActionResult> Repair(ulong constructId)
    {
        var provider = ModBase.ServiceProvider;
        var constructService = provider.GetRequiredService<IConstructService>();
        if (!await constructService.ExistsAndNotDeleted(constructId))
            return NotFound(new { error = "Construct not found or deleted", constructId });

        var orleans = provider.GetOrleans();
        var elementsGrain = orleans.GetConstructElementsGrain(constructId);
        var elementIds = (await elementsGrain.GetElementsOfType<Element>()).ToList();
        var elementCount = elementIds.Count;
        foreach (var elementId in elementIds)
        {
            await elementsGrain.UpdateElementProperty(new ElementPropertyUpdate
            {
                timePoint = TimePoint.Now(),
                elementId = elementId,
                constructId = constructId,
                name = "hitpointsRatio",
                value = new PropertyValue(1.0)
            });
        }

        return Ok(new { repaired = true, constructId, elementCount });
    }

    private static ulong? ParseTargetConstructIdFromContext(object ctxVal)
    {
        if (ctxVal == null) return null;
        if (ctxVal is ulong u) return u;
        if (ctxVal is long l && l >= 0) return (ulong)l;
        if (ctxVal is JValue jv && jv.Value != null)
        {
            if (jv.Value is long l2 && l2 >= 0) return (ulong)l2;
            if (jv.Value is ulong u2) return u2;
            if (ulong.TryParse(jv.Value.ToString(), out var parsed)) return parsed;
        }
        if (ulong.TryParse(ctxVal.ToString(), out var p)) return p;
        return null;
    }
}

/// <summary>Optional body for POST /alienwar/claim/{constructId}. If playerId is omitted, the bot player is used.</summary>
public class ClaimConstructRequest
{
    public ulong? PlayerId { get; set; }
}
