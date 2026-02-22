using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mod.DynamicEncounters.Common.Helpers;
using Mod.DynamicEncounters.Features.AlienWar.Data;
using Mod.DynamicEncounters.Features.AlienWar.Interfaces;
using Mod.DynamicEncounters.Features.Common.Interfaces;
using Mod.DynamicEncounters.Features.Interfaces;
using Mod.DynamicEncounters.Features.Scripts.Actions.Data;
using Mod.DynamicEncounters.Features.Scripts.Actions.Interfaces;
using Mod.DynamicEncounters.Features.TaskQueue.Data;
using Mod.DynamicEncounters.Features.TaskQueue.Interfaces;
using Mod.DynamicEncounters.Helpers;
using Mod.DynamicEncounters.Helpers.DU;
using Newtonsoft.Json.Linq;
using NQ;
using NQ.Interfaces;
using NQutils.Def;
using Orleans;

namespace Mod.DynamicEncounters.Features.TaskQueue.Services;

public class TaskQueueService(IServiceProvider provider) : ITaskQueueService
{
    private const string ProcessQueueMessageCountFeatureName = "ProcessQueueMessageCount";

    private readonly ITaskQueueRepository _repository = provider.GetRequiredService<ITaskQueueRepository>();
    private readonly IFeatureReaderService _featureReaderService = provider.GetRequiredService<IFeatureReaderService>();
    private readonly ILogger<TaskQueueService> _logger = provider.CreateLogger<TaskQueueService>();
    
    public async Task ProcessQueueMessages(CancellationToken cancellationToken)
    {
        var messageBatch = await _featureReaderService.GetIntValueAsync(ProcessQueueMessageCountFeatureName, 10);

        var messages = (await _repository.FindNextAsync(messageBatch)).ToList();
        
        _logger.LogDebug("{Count} messages to process", messages.Count);

        var taskList = new List<Task>();

        foreach (var message in messages)
        {
            if (cancellationToken.IsCancellationRequested) return;
            
            switch (message.Command)
            {
                case "script":
                    var scriptActionFactory = provider.GetRequiredService<IScriptActionFactory>();
                    var scriptActionItem = JToken.FromObject(message.Data).ToObject<ScriptActionItem>();

                    var scriptAction = scriptActionFactory.Create(scriptActionItem);

                    HashSet<ulong> playerIds = [];
                    if (scriptActionItem.Properties.TryGetValue("PlayerIds", out JArray playerIdsJArray))
                    {
                        foreach (var item in playerIdsJArray)
                        {
                            playerIds.Add(item.Value<ulong>());
                        }
                    }

                    var context = new ScriptContext(
                        provider,
                        scriptActionItem.FactionId,
                        [..playerIds],
                        scriptActionItem.Sector ?? new Vec3(),
                        scriptActionItem.TerritoryId
                    )
                    {
                        ConstructId = scriptActionItem.ConstructId
                    };
                    
                    context.AddProperties(scriptActionItem.Properties);
                    
                    var task = scriptAction.ExecuteAsync(context).OnError(exception =>
                    {
                        _logger.LogError(exception, "Failed to Dequeue Script Task");
                        foreach (var e in exception.InnerExceptions)
                        {
                            _logger.LogError(e, "Inner Exception");
                        }
                    });
                    
                    taskList.Add(task);
                    taskList.Add(_repository.TagCompleted(message.Id));
                    
                    break;
                case "alienwar-check":
                    taskList.Add(ProcessAlienWarCheckAsync(message));
                    break;
                default:
                    _logger.LogWarning("Command Type {Command} Not implemented. Message Ignored", message.Command);
                    taskList.Add(_repository.TagFailed(message.Id));
                    break;
            }
        }

        try
        {
            await Task.WhenAll(taskList);

            if (messages.Count > 0)
            {
                _logger.LogInformation("Processed {Count} messages", messages.Count);
            }
        }
        catch (AggregateException ae)
        {
            _logger.LogError(ae, "One or more tasks failed to execute");

            foreach (var exception in ae.InnerExceptions)
            {
                _logger.LogError(exception, "Failed to execute task");
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to execute task queue processing");
        }
    }

    public Task EnqueueScript(ScriptActionItem script, DateTime? deliveryAt)
    {
        return _repository.AddAsync(
            new TaskQueueItem
            {
                Id = Guid.NewGuid(),
                Command = "script",
                DeliveryAt = deliveryAt ?? DateTime.UtcNow,
                Data = script,
                Status = "scheduled"
            }
        );
    }

    public Task EnqueueAlienWarCheck(AlienWarCheckTaskData data, DateTime deliveryAt)
    {
        return _repository.AddAsync(
            new TaskQueueItem
            {
                Id = Guid.NewGuid(),
                Command = "alienwar-check",
                DeliveryAt = deliveryAt,
                Data = data,
                Status = "scheduled"
            }
        );
    }

    private async Task ProcessAlienWarCheckAsync(TaskQueueItem message)
    {
        try
        {
            var data = (message.Data as JToken)?.ToObject<AlienWarCheckTaskData>();
            if (data == null)
            {
                _logger.LogWarning("AlienWar check task has invalid data");
                await _repository.TagFailed(message.Id);
                return;
            }

            var constructService = provider.GetRequiredService<IConstructService>();
            var constructHandleRepo = provider.GetRequiredService<IConstructHandleRepository>();
            var constructElementsService = provider.GetRequiredService<IConstructElementsService>();
            var shieldService = provider.GetRequiredService<IAlienCoreShieldService>();
            var stateService = provider.GetRequiredService<IAlienWarStateService>();
            var orleans = provider.GetOrleans();

            var eventRepo = provider.GetRequiredService<IAlienWarEventRepository>();

            if (!await constructService.ExistsAndNotDeleted(data.CoreConstructId))
            {
                _logger.LogInformation("AlienWar: Core construct {CoreId} no longer exists, ending event", data.CoreConstructId);
                await DespawnAlienWarHandlesAndEndEvent(constructHandleRepo, orleans, data.Sector, data.CoreConstructId, stateService, eventRepo);
                await _repository.TagCompleted(message.Id);
                return;
            }

            var shieldStatus = await shieldService.GetShieldStatusAsync(data.CoreConstructId, data.CooldownSecondsOverride);
            if (shieldStatus == null)
            {
                await _repository.TagFailed(message.Id);
                return;
            }

            var phase = shieldStatus.IsInLockdown ? AlienWarPhase.Guard : AlienWarPhase.Attack;
            stateService.SetState(data.CoreConstructId, new AlienWarEventState
            {
                CoreConstructId = data.CoreConstructId,
                Sector = data.Sector,
                ScriptName = data.ScriptName,
                Phase = phase,
                LockdownEndAtUtc = shieldStatus.LockdownExitAtUtc
            });

            var handles = (await constructHandleRepo.FindAlienWarHandlesInSectorAsync(data.Sector, data.CoreConstructId)).ToList();
            var aliveHandles = new List<ConstructHandleItem>();
            foreach (var h in handles)
            {
                if (await constructService.ExistsAndNotDeleted(h.ConstructId))
                    aliveHandles.Add(h);
            }

            if (aliveHandles.Count == 0)
            {
                _logger.LogInformation("AlienWar: All spawns destroyed for core {CoreId}, ending event", data.CoreConstructId);
                stateService.RemoveState(data.CoreConstructId);
                await eventRepo.RemoveByCoreAsync(data.CoreConstructId);
                await _repository.TagCompleted(message.Id);
                return;
            }

            // If bots destroyed the core: claim it to the bot player and repair all elements (shield/elements to full), then end event
            var coreUnitId = await constructElementsService.GetCoreUnit(data.CoreConstructId);
            if (coreUnitId != 0)
            {
                try
                {
                    var coreUnitInfo = await constructElementsService.GetElement(data.CoreConstructId, coreUnitId);
                    if (coreUnitInfo.IsCoreDestroyed())
                    {
                        _logger.LogInformation("AlienWar: Core {CoreId} destroyed by bots, claiming and repairing", data.CoreConstructId);
                        var botPlayerId = aliveHandles[0].OriginalOwnerPlayerId;
                        var constructGrain = orleans.GetConstructGrain(data.CoreConstructId);
                        // Pass new owner as first arg (like AkimboAdminMod takeover); passing 0 can be treated as disown/destroy.
                        await constructGrain.ConstructSetOwner(botPlayerId, new ConstructOwnerSet { ownerId = new EntityId { playerId = botPlayerId } }, doKeyCheck: false);

                        var elementsGrain = orleans.GetConstructElementsGrain(data.CoreConstructId);
                        var elementIds = await elementsGrain.GetElementsOfType<Element>();
                        foreach (var elementId in elementIds)
                        {
                            await elementsGrain.UpdateElementProperty(new ElementPropertyUpdate
                            {
                                timePoint = TimePoint.Now(),
                                elementId = elementId,
                                constructId = data.CoreConstructId,
                                name = "hitpointsRatio",
                                value = new PropertyValue(1.0)
                            });
                        }

                        await DespawnAlienWarHandlesAndEndEvent(constructHandleRepo, orleans, data.Sector, data.CoreConstructId, stateService, eventRepo);
                        await _repository.TagCompleted(message.Id);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "AlienWar: Failed to check or handle core destruction for {CoreId}", data.CoreConstructId);
                }
            }

            var nextCheck = DateTime.UtcNow.AddSeconds(30);
            if (phase == AlienWarPhase.Guard && shieldStatus.LockdownExitAtUtc.HasValue && shieldStatus.LockdownExitAtUtc.Value < nextCheck)
                nextCheck = shieldStatus.LockdownExitAtUtc.Value.AddSeconds(1);

            await EnqueueAlienWarCheck(data, nextCheck);
            await _repository.TagCompleted(message.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AlienWar check task failed");
            await _repository.TagFailed(message.Id);
        }
    }

    private static async Task DespawnAlienWarHandlesAndEndEvent(
        IConstructHandleRepository constructHandleRepo,
        IClusterClient orleans,
        Vec3 sector,
        ulong coreConstructId,
        IAlienWarStateService stateService,
        IAlienWarEventRepository eventRepo)
    {
        var handles = (await constructHandleRepo.FindAlienWarHandlesInSectorAsync(sector, coreConstructId)).ToList();
        var parentingGrain = orleans.GetConstructParentingGrain();
        foreach (var h in handles)
        {
            try
            {
                await parentingGrain.DeleteConstruct(h.ConstructId, hardDelete: true);
            }
            catch
            {
                // Ignore per-construct errors
            }
        }

        await constructHandleRepo.TagAsDeletedConstructHandledThatAreDeletedConstructs();
        stateService.RemoveState(coreConstructId);
        await eventRepo.RemoveByCoreAsync(coreConstructId);
    }

    private static bool _alienWarResumeDone;

    public async Task<bool> CancelAlienWarEventAsync(ulong coreConstructId)
    {
        var eventRepo = provider.GetRequiredService<IAlienWarEventRepository>();
        var active = await eventRepo.GetActiveAsync();
        var evt = active.FirstOrDefault(e => e.CoreConstructId == coreConstructId);
        if (evt == null)
            return false;
        var constructHandleRepo = provider.GetRequiredService<IConstructHandleRepository>();
        var orleans = provider.GetOrleans();
        var stateService = provider.GetRequiredService<IAlienWarStateService>();
        await DespawnAlienWarHandlesAndEndEvent(constructHandleRepo, orleans, evt.Sector, evt.CoreConstructId, stateService, eventRepo);
        return true;
    }

    public async Task ResumeAlienWarEventsIfNeededAsync()
    {
        if (_alienWarResumeDone)
            return;
        _alienWarResumeDone = true;
        try
        {
            var eventRepo = provider.GetRequiredService<IAlienWarEventRepository>();
            var active = await eventRepo.GetActiveAsync();
            foreach (var evt in active)
            {
                await EnqueueAlienWarCheck(
                    new AlienWarCheckTaskData
                    {
                        CoreConstructId = evt.CoreConstructId,
                        Sector = evt.Sector,
                        ScriptName = evt.ScriptName,
                        CooldownSecondsOverride = evt.CooldownSecondsOverride
                    },
                    DateTime.UtcNow);
            }
            if (active.Count > 0)
                _logger.LogInformation("AlienWar: Resumed {Count} active event(s) after startup", active.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AlienWar: Failed to resume events on startup");
            _alienWarResumeDone = false;
        }
    }
}