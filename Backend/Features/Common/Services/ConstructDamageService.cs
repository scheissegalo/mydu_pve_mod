using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mod.DynamicEncounters.Features.Common.Data;
using Mod.DynamicEncounters.Features.Common.Interfaces;
using Mod.DynamicEncounters.Helpers;
using NQutils.Def;

namespace Mod.DynamicEncounters.Features.Common.Services;

public class ConstructDamageService(IServiceProvider provider) : IConstructDamageService
{
    private readonly IConstructElementsService _constructElementsService =
        provider.GetRequiredService<IConstructElementsService>();

    private readonly IGameplayBank _bank = provider.GetGameplayBank();
    private readonly ILogger<ConstructDamageService> _logger = provider.GetRequiredService<ILogger<ConstructDamageService>>();

    private Dictionary<WeaponTypeScale, IList<AmmoItem>>? AmmoMap { get; set; }

    public Dictionary<WeaponTypeScale, IList<AmmoItem>> GetAllAmmoTypesByWeapon()
    {
        if (AmmoMap != null)
        {
            return AmmoMap;
        }

        var dictionary = new Dictionary<WeaponTypeScale, IList<AmmoItem>>();

        var ammo = _bank.GetDefinition<Ammo>();

        foreach (var itemId in ammo.GetChildrenIdsRecursive())
        {
            var bo = _bank.GetBaseObject<Ammo>(itemId);
            if (bo == null || bo.Hidden)
            {
                continue;
            }

            var def = _bank.GetDefinition(itemId);
            if (def == null || def.GetChildren().Any())
            {
                continue;
            }

            var key = new WeaponTypeScale(bo.WeaponType, bo.Scale);

            dictionary.TryAdd(
                key,
                new List<AmmoItem>()
            );

            dictionary[key].Add(new AmmoItem(
                def.Id,
                def.Name,
                bo
            ));
        }

        AmmoMap = dictionary;

        return AmmoMap;
    }

    public async Task<ConstructDamageData> GetConstructDamage(ulong constructId)
    {
        var weaponUnits = (await _constructElementsService.GetWeaponUnits(constructId)).ToList();

        if (weaponUnits.Count == 0)
        {
            _logger.LogWarning("ConstructDamageService[{Construct}]: No weapon units found on construct. " +
                "This means GetElementsOfType<WeaponUnit>() returned no results. " +
                "Check if the construct has weapons in the blueprint JSON.",
                constructId);
            return new ConstructDamageData([]);
        }

        _logger.LogWarning("ConstructDamageService[{Construct}]: Processing {WeaponCount} weapon units", constructId, weaponUnits.Count);

        var allAmmo = GetAllAmmoTypesByWeapon();
        var items = new List<WeaponItem>();

        foreach (var weaponUnit in weaponUnits)
        {
            var element = await _constructElementsService.GetElement(constructId, weaponUnit.elementId);

            var baseObject = _bank.GetBaseObject<WeaponUnit>(element.elementType);
            var def = _bank.GetDefinition(element);

            if (baseObject == null) continue;

            var ammoKey = new WeaponTypeScale(baseObject.WeaponType, baseObject.Scale);
            if (allAmmo.TryGetValue(ammoKey, out var ammoItems))
            {
                items.Add(new WeaponItem(
                    element.elementId,
                    def.Name,
                    baseObject,
                    ammoItems
                ));
            }
        }

        return new ConstructDamageData(
            items.DistinctBy(x => x.ItemTypeName)
                .Where(x => x.BaseDamage > 0)
        );

        // Log all weapon element types found first

        // var allWeaponTypes = new List<string>();
        // foreach (var weaponUnit in weaponUnits)
        // {
        //     var element = await _constructElementsService.GetElement(constructId, weaponUnit.elementId);
        //     var def = _bank.GetDefinition(element);
        //     var baseObject = _bank.GetBaseObject<WeaponUnit>(element.elementType);
        //     var weaponName = def?.Name ?? $"ElementType_{element.elementType.ToString()}";
        //     var weaponType = baseObject != null ? baseObject.WeaponType : 0;
        //     var weaponScale = baseObject != null ? baseObject.Scale : "0";
        //     var baseDamage = baseObject != null ? baseObject.BaseDamage : 0;
        //     allWeaponTypes.Add($"{weaponName} (WeaponType: {weaponType}, Scale: {weaponScale}, BaseDamage: {baseDamage})");
        // }
        // _logger.LogWarning("ConstructDamageService[{Construct}]: All weapon units found: {WeaponTypes}",
        //     constructId, string.Join(" | ", allWeaponTypes));

        // var allAmmo = GetAllAmmoTypesByWeapon();
        // var items = new List<WeaponItem>();
        // var skippedNoBaseObject = 0;
        // var skippedNoAmmo = 0;
        // var skippedZeroDamage = 0;

        // foreach (var weaponUnit in weaponUnits)
        // {
        //     var element = await _constructElementsService.GetElement(constructId, weaponUnit.elementId);

        //     var baseObject = _bank.GetBaseObject<WeaponUnit>(element.elementType);
        //     var def = _bank.GetDefinition(element);

        //     var weaponName = def?.Name ?? $"ElementType_{element.elementType.ToString()}";
        //     var weaponType = baseObject != null ? baseObject.WeaponType : 0;
        //     var weaponScale = baseObject != null ? baseObject.Scale : "0";

        //     if (baseObject == null)
        //     {
        //         skippedNoBaseObject++;
        //         _logger.LogDebug("ConstructDamageService[{Construct}]: Weapon unit {ElementId} (type {ElementType}, name: {WeaponName}) has no base object", 
        //             constructId, weaponUnit.elementId, element.elementType, weaponName);
        //         continue;
        //     }

        //     _logger.LogDebug("ConstructDamageService[{Construct}]: Processing weapon {WeaponName} (WeaponType: {WeaponType}, Scale: {Scale}, BaseDamage: {BaseDamage})",
        //         constructId, weaponName, weaponType, weaponScale, baseObject.BaseDamage);

        //     var ammoKey = new WeaponTypeScale(baseObject.WeaponType, baseObject.Scale);
        //     if (!allAmmo.TryGetValue(ammoKey, out var ammoItems))
        //     {
        //         skippedNoAmmo++;
        //         _logger.LogWarning("ConstructDamageService[{Construct}]: Weapon {WeaponName} (type {WeaponType}, scale {Scale}) has no matching ammo. " +
        //             "This weapon type/scale combination is not supported. Available ammo keys: {AvailableKeys}. " +
        //             "NOTE: If this is a LASER weapon, lasers may not use ammo - check if special handling is needed.",
        //             constructId, weaponName, baseObject.WeaponType, baseObject.Scale,
        //             string.Join(", ", allAmmo.Keys.Select(k => $"{k.WeaponType}/{k.Scale}")));
        //         continue;
        //     }

        //     var weaponItem = new WeaponItem(
        //         element.elementId,
        //         def.Name,
        //         baseObject, 
        //         ammoItems
        //     );

        //     if (weaponItem.BaseDamage <= 0)
        //     {
        //         skippedZeroDamage++;
        //         _logger.LogWarning("ConstructDamageService[{Construct}]: Weapon {WeaponName} (WeaponType: {WeaponType}, Scale: {Scale}) has BaseDamage <= 0 ({BaseDamage}). " +
        //             "This is a utility weapon (like Stasis) and cannot be used for combat.",
        //             constructId, weaponName, baseObject.WeaponType, baseObject.Scale, weaponItem.BaseDamage);
        //         continue;
        //     }

        //     items.Add(weaponItem);
        //     _logger.LogDebug("ConstructDamageService[{Construct}]: Added weapon {WeaponName} (BaseDamage: {BaseDamage}, WeaponType: {WeaponType}, Scale: {Scale})", 
        //         constructId, weaponName, weaponItem.BaseDamage, baseObject.WeaponType, baseObject.Scale);
        // }

        // var distinctItems = items.DistinctBy(x => x.ItemTypeName).ToList();
        // var validWeapons = distinctItems.Where(x => x.BaseDamage > 0).ToList();
        // var result = new ConstructDamageData(validWeapons);

        // _logger.LogWarning("ConstructDamageService[{Construct}]: Created DamageData with {WeaponCount} weapons: {WeaponNames}",
        //     constructId, validWeapons.Count, string.Join(", ", validWeapons.Select(w => w.ItemTypeName)));

        // _logger.LogWarning("ConstructDamageService[{Construct}]: Processed {Total} weapon units -> {Valid} valid weapons. " +
        //     "Skipped: {SkippedBaseObject} (no base object), {SkippedAmmo} (no ammo), {SkippedDamage} (zero damage).",
        //     constructId, weaponUnits.Count, result.Weapons.Count(), skippedNoBaseObject, skippedNoAmmo, skippedZeroDamage);

        // return result;
    }
}