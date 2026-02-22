using System;
using System.Threading;
using System.Threading.Tasks;
using Mod.DynamicEncounters.Features.AlienWar.Data;
using Mod.DynamicEncounters.Features.Scripts.Actions.Data;

namespace Mod.DynamicEncounters.Features.TaskQueue.Interfaces;

public interface ITaskQueueService
{
    Task ProcessQueueMessages(CancellationToken cancellationToken);
    Task EnqueueScript(ScriptActionItem script, DateTime? deliveryAt);
    Task EnqueueAlienWarCheck(AlienWarCheckTaskData data, DateTime deliveryAt);
    Task ResumeAlienWarEventsIfNeededAsync();
    /// <summary>Despawns all Alien War bots for the given core and removes the event. Returns true if an event was cancelled.</summary>
    Task<bool> CancelAlienWarEventAsync(ulong coreConstructId);
}