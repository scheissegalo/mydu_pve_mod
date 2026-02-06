using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Backend.Scenegraph;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mod.DynamicEncounters.Common.Data;
using Mod.DynamicEncounters.Common.Interfaces;
using Mod.DynamicEncounters.Features.Common.Data;
using Mod.DynamicEncounters.Features.Common.Interfaces;
using Mod.DynamicEncounters.Features.Scripts.Actions.Interfaces;
using Mod.DynamicEncounters.Features.Spawner.Behaviors.Data;
using Mod.DynamicEncounters.Features.Spawner.Behaviors.Interfaces;
using Mod.DynamicEncounters.Features.Spawner.Data;
using Mod.DynamicEncounters.Features.VoxelService.Data;
using Mod.DynamicEncounters.Features.VoxelService.Interfaces;
using Mod.DynamicEncounters.Helpers;
using NQ;
using NQ.Interfaces;
using NQutils.Def;
using Orleans;

namespace Mod.DynamicEncounters.Features.Spawner.Behaviors;

public class AggressiveBehavior(ulong constructId, IPrefab prefab) : IConstructBehavior
{
    // Alien cores (resource nodes) that should never be attacked
    private static readonly HashSet<ulong> ExcludedConstructIds = new()
    {
        990001, 990002, 990003, 990004, 990005,
        990006, 990007, 990008, 990009, 990010
    };

    private IClusterClient _orleans;
    private ILogger<AggressiveBehavior> _logger;
    private IConstructService _constructService;

    private ElementId _coreUnitElementId;

    private bool _active = true;
    private IConstructElementsService _constructElementsService;
    private IVoxelServiceClient _pveVoxelService;
    private IScenegraph _sceneGraph;

    public bool IsActive() => _active;

    public BehaviorTaskCategory Category => BehaviorTaskCategory.HighPriority;

    public async Task InitializeAsync(BehaviorContext context)
    {
        var provider = context.Provider;
        _orleans = provider.GetOrleans();

        _constructElementsService = provider.GetRequiredService<IConstructElementsService>();
        _coreUnitElementId = await _constructElementsService.GetCoreUnit(constructId);
        _constructService = provider.GetRequiredService<IConstructService>();
        _pveVoxelService = provider.GetRequiredService<IVoxelServiceClient>();
        _sceneGraph = provider.GetRequiredService<IScenegraph>();

        context.Properties.TryAdd("CORE_ID", _coreUnitElementId);

        _logger = provider.CreateLogger<AggressiveBehavior>();
    }

    public async Task TickAsync(BehaviorContext context)
    {
        // Check if DamageData has weapons
        if (context.DamageData == null)
        {
            return; // Too early, wait for AliveCheckBehavior
        }

        var weaponsList = context.DamageData.Weapons.ToList();
        if (!weaponsList.Any())
        {
            var lifeTimeSpan = DateTime.UtcNow - context.StartedAt;
            if (lifeTimeSpan < TimeSpan.FromSeconds(4))
            {
                return; // Too early, wait for AliveCheckBehavior
            }

            _logger.LogWarning("AggressiveBehavior[{Construct}]: No weapons in DamageData after {Age}s",
                constructId, lifeTimeSpan.TotalSeconds);
            return;
        }

        if (!context.IsAlive)
        {
            _active = false;
            _logger.LogDebug("AggressiveBehavior[{Construct}]: Construct is not alive, deactivating", constructId);
            return;
        }

        var targetConstructId = context.GetTargetConstructId();

        if (!targetConstructId.HasValue)
        {
            return; // No target selected yet
        }

        // Never attack excluded constructs (alien cores)
        if (ExcludedConstructIds.Contains(targetConstructId.Value))
        {
            return; // Skip excluded constructs
        }

        var provider = context.Provider;

        var npcShotGrain = _orleans.GetNpcShotGrain();

        var constructInfoOutcome = await _constructService.GetConstructInfoAsync(constructId);
        var constructInfo = constructInfoOutcome.Info;
        if (constructInfo == null)
        {
            _logger.LogWarning("AggressiveBehavior[{Construct}]: Failed to get construct info", constructId);
            return;
        }

        var constructPos = constructInfo.rData.position;

        if (targetConstructId is null or 0)
        {
            return;
        }

        var targetInfoOutcome = await _constructService.GetConstructInfoAsync(targetConstructId.Value);
        var targetInfo = targetInfoOutcome.Info;
        if (targetInfo == null)
        {
            _logger.LogDebug("AggressiveBehavior[{Construct}]: Failed to get target {Target} info", constructId, targetConstructId.Value);
            return;
        }

        var targetSize = targetInfo.rData.geometry.size;

        if (targetInfo.mutableData.pilot.HasValue)
        {
            context.PlayerIds.Add(targetInfo.mutableData.pilot.Value);
        }

        var random = provider.GetRandomProvider()
            .GetRandom();

        // var hitPos = random.RandomDirectionVec3() * targetSize / 2;
        var hitPos = random.RandomDirectionVec3() * targetSize / 4;
        var constructSize = (ulong)constructInfo.rData.geometry.size;
        var targetPos = targetInfo.rData.position;

        if (!context.Position.HasValue)
        {
            _logger.LogDebug("AggressiveBehavior[{Construct}]: No position available yet", constructId);
            return;
        }

        context.WeaponEffectivenessData = await _constructElementsService.GetDamagingWeaponsEffectiveness(constructId);
        if (!context.HasAnyDamagingWeapons())
        {
            return; // No functional damaging weapons available
        }

        var targetDistance = context.GetTargetPosition().Distance(context.Position.Value);
        var weapon = context.GetBestFunctionalWeaponByTargetDistance(targetDistance);

        if (weapon == null)
        {
            return; // No suitable weapon for this distance
        }

        await ShootAndCycleAsync(
            new ShotContext(
                context,
                npcShotGrain,
                weapon,
                constructPos,
                constructSize,
                targetConstructId.Value,
                targetPos,
                hitPos,
                context.DamageData.Weapons.Count() // One shot equivalent of all weapons for performance reasons
            )
        );
    }

    public class ShotContext(
        BehaviorContext behaviorContext,
        INpcShotGrain npcShotGrain,
        WeaponItem weaponHandle,
        Vec3 constructPosition,
        ulong constructSize,
        ulong targetConstructId,
        Vec3 targetPosition,
        Vec3 hitPosition,
        int quantityModifier
    )
    {
        public BehaviorContext BehaviorContext { get; set; } = behaviorContext;
        public INpcShotGrain NpcShotGrain { get; set; } = npcShotGrain;
        public WeaponItem WeaponItem { get; set; } = weaponHandle;
        public Vec3 ConstructPosition { get; set; } = constructPosition;
        public ulong ConstructSize { get; set; } = constructSize;
        public ulong TargetConstructId { get; set; } = targetConstructId;
        public Vec3 TargetPosition { get; set; } = targetPosition;
        public Vec3 HitPosition { get; set; } = hitPosition;
        public int QuantityModifier { get; set; } = quantityModifier;
    }

    private const string ShotTotalDeltaTimePropName = $"{nameof(AggressiveBehavior)}_ShotTotalDeltaTime";

    private double GetShootTotalDeltaTime(BehaviorContext context)
    {
        if (context.Properties.TryGetValue(ShotTotalDeltaTimePropName, out var value))
        {
            return (double)value;
        }

        return 0;
    }

    private void SetShootTotalDeltaTime(BehaviorContext context, double value)
    {
        if (!context.Properties.TryAdd(ShotTotalDeltaTimePropName, value))
        {
            context.Properties[ShotTotalDeltaTimePropName] = value;
        }
    }

    private async Task ShootAndCycleAsync(ShotContext context)
    {
        var distance = (context.TargetPosition - context.ConstructPosition).Size();

        if (distance > 2 * DistanceHelpers.OneSuInMeters)
        {
            return; // Target too far
        }

        var (functionalCount, totalCount) =
            context.BehaviorContext.GetWeaponEffectivenessFactors(context.WeaponItem.ItemTypeName);

        if (functionalCount == 0 || totalCount == 0)
        {
            return; // No functional weapons available
        }
        var functionalWeaponFactor = Math.Clamp((double)functionalCount / totalCount, 0d, 1d);

        context.BehaviorContext.FunctionalWeaponFactor = functionalWeaponFactor;

        context.QuantityModifier = functionalCount;
        context.QuantityModifier = Math.Clamp(context.QuantityModifier, 0, prefab.DefinitionItem.MaxWeaponCount);

        var random = context.BehaviorContext.Provider.GetRequiredService<IRandomProvider>()
            .GetRandom();

        var previousDeltaTime = GetShootTotalDeltaTime(context.BehaviorContext);
        var totalDeltaTime = previousDeltaTime + context.BehaviorContext.DeltaTime;

        SetShootTotalDeltaTime(context.BehaviorContext, totalDeltaTime);

        var w = context.WeaponItem;

        var ammoType = w.GetAmmoItems()
            .Where(x => x.Level == prefab.DefinitionItem.AmmoTier &&
                        x.ItemTypeName.Contains(prefab.DefinitionItem.AmmoVariant,
                            StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        if (ammoType.Count == 0)
        {
            _logger.LogError("AggressiveBehavior[{Construct}]: No matching ammo found for weapon {Weapon} (tier: {Tier}, variant: {Variant})",
                constructId, w.ItemTypeName, prefab.DefinitionItem.AmmoTier, prefab.DefinitionItem.AmmoVariant);
            return;
        }

        var ammoItem = random.PickOneAtRandom(ammoType);
        var mod = prefab.DefinitionItem.Mods;

        context.BehaviorContext.ShotWaitTime = w.GetShotWaitTimePerGun(
            ammoItem,
            weaponCount: context.QuantityModifier,
            cycleTimeBuffFactor: mod.Weapon.CycleTime
        );

        if (totalDeltaTime < context.BehaviorContext.ShotWaitTime)
        {
            return; // Waiting for shot cooldown
        }

        var isInSafeZone = await _constructService.IsInSafeZone(constructId);
        if (isInSafeZone)
        {
            SetShootTotalDeltaTime(context.BehaviorContext, 0);
            return;
        }

        if (context.TargetConstructId > 0)
        {
            var targetInSafeZone = await _constructService.IsInSafeZone(context.TargetConstructId);
            if (targetInSafeZone)
            {
                SetShootTotalDeltaTime(context.BehaviorContext, 0);
                return;
            }
        }

        var relativeLocation = await _sceneGraph.ResolveRelativeLocation(
            new RelativeLocation { position = context.ConstructPosition, rotation = Quat.Identity },
            context.TargetConstructId
        );

        var shootPointOutcome = await _pveVoxelService.QueryRandomPoint(
            new QueryRandomPoint
            {
                ConstructId = context.TargetConstructId,
                FromLocalPosition = relativeLocation.position
            }
        );
        if (shootPointOutcome.Success)
        {
            context.HitPosition = shootPointOutcome.LocalPosition;
            _logger.LogError("Hit Pos: {Pos}", context.HitPosition);
        }
        else
        {
            context.HitPosition = random.RandomDirectionVec3() * context.ConstructSize * 2;
        }

        SetShootTotalDeltaTime(context.BehaviorContext, 0);

        var weapon = new SentinelWeapon
        {
            aoe = true,
            damage = w.BaseDamage * mod.Weapon.Damage,
            range = w.BaseOptimalDistance * mod.Weapon.OptimalDistance +
                    w.FalloffDistance * mod.Weapon.FalloffDistance,
            aoeRange = 100000,
            baseAccuracy = w.BaseAccuracy * mod.Weapon.Accuracy,
            effectDuration = 10,
            effectStrength = 10,
            falloffDistance = w.FalloffDistance * mod.Weapon.FalloffDistance,
            falloffTracking = w.FalloffTracking * mod.Weapon.FalloffTracking,
            fireCooldown = context.BehaviorContext.ShotWaitTime,
            baseOptimalDistance = w.BaseOptimalDistance * mod.Weapon.OptimalDistance,
            falloffAimingCone = w.FalloffAimingCone * mod.Weapon.FalloffAimingCone,
            baseOptimalTracking = w.BaseOptimalTracking * mod.Weapon.OptimalTracking,
            baseOptimalAimingCone = w.BaseOptimalAimingCone * mod.Weapon.OptimalAimingCone,
            optimalCrossSectionDiameter = w.OptimalCrossSectionDiameter,
            ammoItem = ammoItem.ItemTypeName,
            weaponItem = w.ItemTypeName
        };

        // TriggerModAction routing is broken - actions aren't reaching MyDuMod.TriggerAction
        // So we'll use PropagateShotImpact directly, which is what actually fires shots and applies damage

        if (context.BehaviorContext.CustomActionShootEnabled)
        {
            var shootWeaponData = new ShootWeaponData
            {
                Weapon = weapon,
                CrossSection = 5,
                ShooterName = w.DisplayName,
                ShooterPosition = context.ConstructPosition,
                ShooterConstructId = constructId,
                LocalHitPosition = context.HitPosition,
                ShooterConstructSize = context.ConstructSize,
                ShooterPlayerId = ModBase.Bot.PlayerId,
                TargetConstructId = context.TargetConstructId,
                DamagesVoxel = context.BehaviorContext.DamagesVoxel
            };

            var modManagerGrain = _orleans.GetModManagerGrain();
            await modManagerGrain.TriggerModAction(
                ModBase.Bot.PlayerId,
                new ActionBuilder()
                    .ShootWeapon(shootWeaponData)
                    .WithConstructId(constructId)
                    .Build()
            );
        }
        else
        {
            await context.NpcShotGrain.Fire(
                w.DisplayName,
                context.ConstructPosition,
                constructId,
                context.ConstructSize,
                context.TargetConstructId,
                context.TargetPosition,
                weapon,
                5,
                context.HitPosition
            );
        }

        _logger.LogInformation("Construct {Construct} Shot Weapon. {Weapon} / {Ammo}",
    constructId,
    w.ItemTypeName,
    ammoItem.ItemTypeName
);


        // try
        // {
        //     var bank = context.BehaviorContext.Provider.GetGameplayBank();
        //     var directServiceGrain = _orleans.GetDirectServiceGrain();

        //     // Get weapon position
        //     var shooterConstructInfoGrain = _orleans.GetConstructInfoGrain(constructId);
        //     var shooterInfo = await shooterConstructInfoGrain.Get();
        //     var shooterRot = shooterInfo.rData.rotation;

        //     var weaponUnits = await _constructElementsService.GetWeaponUnits(constructId);
        //     var firstWeapon = weaponUnits.FirstOrDefault();

        //     Vec3 shooterWeaponLocalPos;
        //     Vec3 shooterWeaponPos;
        //     if (firstWeapon != default(ElementId))
        //     {
        //         var firstWeaponElementInfo = await _constructElementsService.GetElement(constructId, firstWeapon);
        //         shooterWeaponLocalPos = firstWeaponElementInfo.position;
        //         var weaponPosVec3 = VectorMathHelper.CalculateWorldPosition(
        //             shooterWeaponLocalPos.ToVector3(),
        //             context.ConstructPosition.ToVector3(),
        //             new System.Numerics.Quaternion((float)shooterRot.x, (float)shooterRot.y, (float)shooterRot.z, (float)shooterRot.w)
        //         );
        //         shooterWeaponPos = new Vec3 { x = weaponPosVec3.X, y = weaponPosVec3.Y, z = weaponPosVec3.Z };
        //     }
        //     else
        //     {
        //         // Fallback to construct center if no weapon found
        //         shooterWeaponLocalPos = new Vec3 { x = 0, y = 0, z = 0 };
        //         shooterWeaponPos = context.ConstructPosition;
        //     }

        //     // Get target construct center position for hit calculation
        //     var targetPosition = await _sceneGraph.GetConstructCenterWorldPosition(context.TargetConstructId);
        //     var targetConstructInfoGrain = _orleans.GetConstructInfoGrain(context.TargetConstructId);
        //     var targetInfo = await targetConstructInfoGrain.Get();
        //     var targetRot = targetInfo.rData.rotation;
        //     var targetSize = targetInfo.rData.geometry.size; // This is a double (scalar size)

        //     // Add random offset to hit position for visual variety (still within construct bounds)
        //     // Use 30% of construct size as max offset to ensure we still hit the ship
        //     var hitRandom = context.BehaviorContext.Provider.GetRequiredService<IRandomProvider>().GetRandom();
        //     var maxOffset = targetSize * 0.3; // 30% of construct size
        //     var offsetX = (hitRandom.NextDouble() - 0.5) * maxOffset;
        //     var offsetY = (hitRandom.NextDouble() - 0.5) * maxOffset;
        //     var offsetZ = (hitRandom.NextDouble() - 0.5) * maxOffset;

        //     // Calculate hit position in local space with offset
        //     var hitPositionLocalVec3 = VectorMathHelper.CalculateRelativePosition(
        //         targetPosition.ToVector3(),
        //         targetInfo.rData.position.ToVector3(),
        //         new System.Numerics.Quaternion((float)targetRot.x, (float)targetRot.y, (float)targetRot.z, (float)targetRot.w)
        //     );

        //     // Apply random offset in local space
        //     hitPositionLocalVec3 = new System.Numerics.Vector3(
        //         hitPositionLocalVec3.X + (float)offsetX,
        //         hitPositionLocalVec3.Y + (float)offsetY,
        //         hitPositionLocalVec3.Z + (float)offsetZ
        //     );

        //     var hitPositionLocal = new Vec3 { x = hitPositionLocalVec3.X, y = hitPositionLocalVec3.Y, z = hitPositionLocalVec3.Z };

        //     // Convert back to world position for the actual hit
        //     var hitPositionWorldVec3 = VectorMathHelper.CalculateWorldPosition(
        //         hitPositionLocalVec3,
        //         targetInfo.rData.position.ToVector3(),
        //         new System.Numerics.Quaternion((float)targetRot.x, (float)targetRot.y, (float)targetRot.z, (float)targetRot.w)
        //     );
        //     var hitPositionWorld = new Vec3 { x = hitPositionWorldVec3.X, y = hitPositionWorldVec3.Y, z = hitPositionWorldVec3.Z };

        //     // Get weapon and ammo IDs
        //     var weaponDef = bank.GetDefinition(weapon.weaponItem);
        //     var ammoDef = bank.GetDefinition(weapon.ammoItem);

        //     if (weaponDef == null || ammoDef == null)
        //     {
        //         _logger.LogError("AggressiveBehavior[{Construct}]: Could not find weapon or ammo definition - weapon: {Weapon}, ammo: {Ammo}",
        //             constructId, weapon.weaponItem, weapon.ammoItem);
        //         return;
        //     }

        //     // Calculate hit ratio to determine if shot hits
        //     var npcCenterPosition = await _sceneGraph.GetConstructCenterWorldPosition(constructId);
        //     var hitRatio = CalculateHitRatio(
        //         npcCenterPosition,
        //         targetPosition,
        //         weapon,
        //         hitPositionWorld,
        //         5.0 // crossSection
        //     );

        //     var hitRoll = new Random().NextDouble();
        //     var isHit = hitRoll <= hitRatio;

        //     // Apply damage if hit and collect results for WeaponShot
        //     double shieldDamage = 0;
        //     double rawShieldDamage = 0;
        //     bool coreUnitDestroyed = false;

        //     if (isHit)
        //     {
        //         var targetConstructFightGrain = _orleans.GetConstructFightGrain(context.TargetConstructId);
        //         var hitResult = await targetConstructFightGrain.ConstructTakeHit(new WeaponShotPower
        //         {
        //             ammoType = ammoDef.Id,
        //             power = weapon.damage,
        //             originPlayerId = ModBase.Bot.PlayerId,
        //             originConstructId = constructId
        //         });

        //         shieldDamage = hitResult.shieldDamage;
        //         rawShieldDamage = hitResult.rawShieldDamage;
        //         coreUnitDestroyed = hitResult.coreUnitStressDestroyed;
        //     }

        //     // Create WeaponShot and propagate - this shows the visual effect
        //     var weaponShot = new WeaponShot
        //     {
        //         id = (ulong)TimePoint.Now().networkTime,
        //         originConstructId = constructId,
        //         originPositionWorld = shooterWeaponPos,
        //         originPositionLocal = shooterWeaponLocalPos,
        //         targetConstructId = context.TargetConstructId,
        //         weaponType = weaponDef.Id,
        //         ammoType = ammoDef.Id,
        //         impactPositionWorld = hitPositionWorld,
        //         impactPositionLocal = hitPositionLocal,
        //         impactElementType = 3,
        //         shieldDamage = shieldDamage,
        //         rawShieldDamage = rawShieldDamage,
        //         coreUnitDestroyed = coreUnitDestroyed
        //     };

        //     await directServiceGrain.PropagateShotImpact(weaponShot);
        // }
        // catch (Exception ex)
        // {
        //     _logger.LogError(ex, "AggressiveBehavior[{Construct}]: PropagateShotImpact failed with exception", constructId);
        //     throw;
        // }

        // try
        // {
        //     var bank = context.BehaviorContext.Provider.GetGameplayBank();
        //     var directServiceGrain = _orleans.GetDirectServiceGrain();

        //     // Get weapon position
        //     var shooterConstructInfoGrain = _orleans.GetConstructInfoGrain(constructId);
        //     var shooterInfo = await shooterConstructInfoGrain.Get();
        //     var shooterRot = shooterInfo.rData.rotation;

        //     var weaponUnits = await _constructElementsService.GetWeaponUnits(constructId);
        //     var firstWeapon = weaponUnits.FirstOrDefault();

        //     Vec3 shooterWeaponLocalPos;
        //     Vec3 shooterWeaponPos;
        //     if (firstWeapon != default(ElementId))
        //     {
        //         var firstWeaponElementInfo = await _constructElementsService.GetElement(constructId, firstWeapon);
        //         shooterWeaponLocalPos = firstWeaponElementInfo.position;
        //         var weaponPosVec3 = VectorMathHelper.CalculateWorldPosition(
        //             shooterWeaponLocalPos.ToVector3(),
        //             context.ConstructPosition.ToVector3(),
        //             shooterRot.ToQuat()
        //         );
        //         shooterWeaponPos = new Vec3 { x = weaponPosVec3.X, y = weaponPosVec3.Y, z = weaponPosVec3.Z };
        //     }
        //     else
        //     {
        //         // Fallback to construct center if no weapon found
        //         shooterWeaponLocalPos = new Vec3 { x = 0, y = 0, z = 0 };
        //         shooterWeaponPos = context.ConstructPosition;
        //     }

        //     // Resolve hit position to world coordinates
        //     var hitPositionWorld = await _sceneGraph.ResolveWorldLocation(new RelativeLocation
        //     {
        //         constructId = context.TargetConstructId,
        //         position = context.HitPosition
        //     });

        //     // Get weapon and ammo IDs
        //     var weaponDef = bank.GetDefinition(weapon.weaponItem);
        //     var ammoDef = bank.GetDefinition(weapon.ammoItem);

        //     if (weaponDef == null || ammoDef == null)
        //     {
        //         _logger.LogError("AggressiveBehavior[{Construct}]: Could not find weapon or ammo definition - weapon: {Weapon}, ammo: {Ammo}", 
        //             constructId, weapon.weaponItem, weapon.ammoItem);
        //         return;
        //     }

        //     // Create WeaponShot and propagate
        //     var weaponShot = new WeaponShot
        //     {
        //         id = (ulong)TimePoint.Now().networkTime,
        //         originConstructId = constructId,
        //         originPositionWorld = shooterWeaponPos,
        //         originPositionLocal = shooterWeaponLocalPos,
        //         targetConstructId = context.TargetConstructId,
        //         weaponType = weaponDef.Id,
        //         ammoType = ammoDef.Id,
        //         impactPositionWorld = hitPositionWorld.position,
        //         impactPositionLocal = context.HitPosition,
        //         impactElementType = 3,
        //         coreUnitDestroyed = false
        //     };

        //     _logger.LogWarning("AggressiveBehavior[{Construct}]: Propagating shot - weapon: {WeaponId}, ammo: {AmmoId}, target: {Target}", 
        //         constructId, weaponShot.weaponType, weaponShot.ammoType, context.TargetConstructId);

        //     await directServiceGrain.PropagateShotImpact(weaponShot);

        //     _logger.LogWarning("AggressiveBehavior[{Construct}]: PropagateShotImpact completed successfully", constructId);
        // }
        // catch (Exception ex)
        // {
        //     _logger.LogError(ex, "AggressiveBehavior[{Construct}]: PropagateShotImpact failed with exception", constructId);
        //     throw;
        // }

    }

    private double CalculateHitRatio(
        Vec3 weaponWorldLocation,
        Vec3 target,
        SentinelWeapon weaponUnit,
        Vec3 impactWorldLocation,
        double crossSection)
    {
        // Simplified hit ratio calculation - always use a reasonable hit chance
        // For now, use a base accuracy that ensures most shots hit at close range
        var distance = weaponWorldLocation.Dist(impactWorldLocation);
        var distanceOptimalValue = weaponUnit.baseOptimalDistance;
        var distanceFallOffValue = weaponUnit.falloffDistance;

        // Calculate distance factor (1.0 at optimal, decreasing with distance)
        var distanceFactor = 1.0;
        if (distance > distanceOptimalValue)
        {
            var excessDistance = distance - distanceOptimalValue;
            var falloffRange = distanceFallOffValue * 5; // 5x falloff for max range
            distanceFactor = Math.Max(0.0, 1.0 - (excessDistance / falloffRange));
        }

        // Base accuracy with distance falloff
        var baseAccuracy = weaponUnit.baseAccuracy;
        var hitRatio = baseAccuracy * distanceFactor;

        // Ensure minimum hit chance at close range
        if (distance < distanceOptimalValue * 2)
        {
            hitRatio = Math.Max(hitRatio, 0.7); // At least 70% hit chance at close range
        }

        return Math.Min(1.0, hitRatio);
    }
}