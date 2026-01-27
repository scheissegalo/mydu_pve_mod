using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mod.DynamicEncounters.Common.Data;
using Mod.DynamicEncounters.Common.Helpers;
using Mod.DynamicEncounters.Features.Common.Interfaces;
using Mod.DynamicEncounters.Features.Scripts.Actions.Interfaces;
using Mod.DynamicEncounters.Features.Spawner.Behaviors.Interfaces;
using Mod.DynamicEncounters.Features.Spawner.Data;
using Mod.DynamicEncounters.Features.Spawner.Extensions;
using Mod.DynamicEncounters.Helpers;
using Mod.DynamicEncounters.Helpers.DU;
using Mod.DynamicEncounters.Threads.Handles;
using NQ;

namespace Mod.DynamicEncounters.Features.Spawner.Behaviors;

public class AliveCheckBehavior(ulong constructId, IPrefab prefab) : IConstructBehavior
{
    private ElementId _coreUnitElementId;

    private IConstructHandleRepository _handleRepository;
    private IConstructService _constructService;
    private IConstructElementsService _constructElementsService;
    private ILogger<AliveCheckBehavior> _logger;
    private IConstructDamageService _constructDamageService;

    public BehaviorTaskCategory Category => BehaviorTaskCategory.HighPriority;

    public async Task InitializeAsync(BehaviorContext context)
    {
        var provider = context.Provider;

        _handleRepository = provider.GetRequiredService<IConstructHandleRepository>();
        _constructService = provider.GetRequiredService<IConstructService>();
        _constructElementsService = provider.GetRequiredService<IConstructElementsService>();
        _constructDamageService = provider.GetRequiredService<IConstructDamageService>();
        _coreUnitElementId = await _constructElementsService.GetCoreUnit(constructId);
        _logger = provider.CreateLogger<AliveCheckBehavior>();
    }

    public async Task TickAsync(BehaviorContext context)
    {
        // waits a bit before evaluating if alive or dead
        var lifeTimeSpan = DateTime.UtcNow - context.StartedAt;
        if (lifeTimeSpan < TimeSpan.FromSeconds(3))
        {
            return;
        }
        
        if (!context.IsAlive || !context.IsBehaviorActive<AliveCheckBehavior>())
        {
            ConstructBehaviorLoop.ConstructHandles.TryRemove(constructId, out _);
            
            try
            {
                var notAliveCoreUnit = await _constructElementsService
                    .NoCache()
                    .GetElement(constructId, _coreUnitElementId)
                    .WithRetry(new RetryOptions<ElementInfo>(3, _logger)
                    {
                        OnRetryAttempt = async _ => await Task.Delay(500)
                    });
                if (notAliveCoreUnit.IsCoreDestroyed())
                {
                    await context.NotifyConstructDestroyedAsync(new BehaviorEventArgs(constructId, prefab, context));
                }
            }
            catch (Exception ex)
            {
                // Handle Orleans serialization errors gracefully
                // These can occur when the construct is being destroyed or Orleans is having issues
                _logger.LogWarning(ex, "AliveCheckBehavior[{Construct}]: Failed to check core unit status (construct may be destroyed or Orleans serialization issue)", constructId);
            }

            _logger.LogInformation("Construct {Construct} NOT ALIVE", constructId);
            await _handleRepository.RemoveHandleAsync(constructId);
            
            return;
        }

        // Always update DamageData - it may have been reset or not set yet
        var weaponUnits = await _constructElementsService.GetWeaponUnits(constructId);
        var weaponUnitsList = weaponUnits.ToList();
        
        var damageData = await _constructDamageService.GetConstructDamage(constructId);
        var weaponsList = damageData.Weapons.ToList();
        
        context.DamageData = damageData;
        
        // Only log if there's an issue (no weapons found when weapon units exist)
        if (!weaponsList.Any() && weaponUnitsList.Any())
        {
            // Only warn if it's been more than 5 seconds - might be a real issue
            if (lifeTimeSpan > TimeSpan.FromSeconds(5))
            {
                _logger.LogWarning("AliveCheckBehavior[{Construct}]: No weapons in DamageData after processing. " +
                    "Weapon units found: {WeaponCount}. This usually means: " +
                    "1) The construct blueprint JSON ({PrefabPath}) doesn't have weapon elements, OR " +
                    "2) The weapons don't match any ammo types (check WeaponType/Scale), OR " +
                    "3) The weapons have BaseDamage <= 0.",
                    constructId, weaponUnitsList.Count, prefab.DefinitionItem.Path);
            }
        }

        // just to cache it
        await Task.WhenAll([
            _constructElementsService.GetAllSpaceEnginesPower(constructId),
            _constructElementsService.GetDamagingWeaponsEffectiveness(constructId)
        ]).OnError(
            exception =>
            {
                _logger.LogError(exception, "Failed to query Weapon and Engines");
                foreach (var e in exception.InnerExceptions)
                {
                    _logger.LogError(e, "Inner Exception");
                }
            }
        );

        var coreUnit = await _constructElementsService
            .NoCache()
            .GetElement(constructId, _coreUnitElementId)
            .WithRetry(new RetryOptions<ElementInfo>(3, _logger)
            {
                OnRetryAttempt = async _ => await Task.Delay(500)
            });;
        var constructInfoOutcome = await _constructService.GetConstructInfoAsync(constructId);
        var constructInfo = constructInfoOutcome.Info;

        if (!constructInfoOutcome.ConstructExists)
        {
            context.IsAlive = false;
            context.Deactivate<AliveCheckBehavior>();
            return;
        }
        
        if (constructInfo == null)
        {
            return;
        }

        if (coreUnit.IsCoreDestroyed())
        {
            await context.NotifyConstructDestroyedAsync(new BehaviorEventArgs(constructId, prefab, context));
            context.Deactivate<AliveCheckBehavior>();
            context.IsAlive = false;

            await _handleRepository.RemoveHandleAsync(constructId);
            
            _logger.LogInformation("Construct {Construct} CORE DESTROYED", constructId);
            
            return;
        }
        
        ConstructBehaviorContextCache.Data.ResetExpiration(constructId);

        await _constructService.ActivateShieldsAsync(constructId);
        
        ConstructBehaviorLoop.RecordConstructHeartBeat(constructId);
    }
}