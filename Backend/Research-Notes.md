# Dynamic Encounters - Research Notes

This document summarizes the investigation so far. It is a reference for scripts
and prefab definitions, with examples.

## Script behavior notes

- `OnSectorEnterScript` is the script that actually runs when players enter a
  sector. It receives the entering `PlayerIds`.
- Spawn actions only execute `Events.OnLoad` right after a construct is spawned.
- `Events.OnSectorEnter` inside a spawn action is not executed anywhere.
- `Type: "random"` picks one of its child actions.
- `Type: "message"` sends a DM to each player in `PlayerIds`.

### Example: OnSectorEnter script with random message + spawn

Use this script as the encounter's `OnSectorEnterScript`.

```json
{
  "Name": "spawn-poi-borg",
  "Actions": [
    {
      "Type": "random",
      "Actions": [
        { "Type": "message", "Message": "We are the Borg!" },
        { "Type": "message", "Message": "Resistance is futile." },
        { "Type": "message", "Message": "You will be assimilated." },
        { "Type": "message", "Message": "Your biological and technological distinctiveness will be added to our own." },
        { "Type": "message", "Message": "Freedom is irrelevant." },
        { "Type": "message", "Message": "You will adapt to service us." }
      ]
    },
    {
      "Type": "spawn",
      "Prefab": "basic-poi",
      "Tags": [ "poi" ],
      "MinQuantity": 1,
      "MaxQuantity": 1,
      "Override": { "ConstructName": "[0] Borg Attack" }
    }
  ]
}
```

### Example: OnLoad event inside a spawn action

Use this only for actions that should run after a construct is created.

```json
{
  "Type": "spawn",
  "Prefab": "basic-poi",
  "Events": {
    "OnLoad": [
      { "Type": "message", "Message": "Construct spawned." }
    ],
    "OnSectorEnter": []
  }
}
```

## Prefab definition behavior notes

### Speed and acceleration

- `MaxSpeedKph` sets the maximum velocity cap.
- Actual movement speed is governed by velocity modifiers and braking logic.
- `AccelerationG` directly affects thrust in movement behavior.
- `Mods.Velocity.*` scales the velocity goal depending on range and direction.
- Booster settings do nothing unless `BoosterActive` is set by logic. The code
  currently does not toggle this, so `BoosterEnabled` alone has no effect.

Practical tips:
- To make NPCs faster, increase `AccelerationG`.
- To remove range-based velocity scaling, set `Mods.Velocity.Enabled` to `false`
  (then velocity goal is `MaxSpeedKph`).

### Weapon count and firing behavior

- The AI selects one weapon type (best for distance) per tick.
- `MaxWeaponCount` caps the weapon count used to shorten cooldown only.
- It does not make the NPC fire multiple weapon units at once.
- The internal per-gun cooldown scaling is capped at 10 anyway.

If you want true multi-weapon volleys, it requires logic changes.

### Weapon modifiers

These are multipliers. Default `1` means no change.

- `Damage`:
  - > 1 = more damage per shot
  - < 1 = less damage per shot
- `CycleTime`:
  - < 1 = faster firing
  - > 1 = slower firing
- `Accuracy`:
  - > 1 = higher hit chance
  - < 1 = lower hit chance
  - At close range there is a minimum hit chance of 70%.

## Full example: Borg cube definition (condensed)

```json
{
  "Name": "borg-cube",
  "Folder": "pve",
  "Path": "Borg_Tactical_Cube.json",
  "IsNpc": false,
  "OwnerId": 4,
  "AmmoTier": 3,
  "AmmoVariant": "Agile",
  "WeaponItems": [
    "WeaponLaserLargePrecision4",
    "WeaponMissileLargePrecision4"
  ],
  "AmmoItems": [
    "AmmoMissileLargeAntimatterAdvancedPrecision",
    "AmmoMissileLargeKineticAdvancedPrecision",
    "AmmoLaserLargeElectromagneticAdvancedPrecision",
    "AmmoLaserLargeThermicAdvancedPrecision"
  ],
  "MaxWeaponCount": 32,
  "AccelerationG": 5,
  "MaxSpeedKph": 50000,
  "RotationSpeed": 0.3,
  "TargetDistance": 20000,
  "InitialBehaviors": [ "aggressive", "follow-target" ],
  "Mods": {
    "Weapon": {
      "Damage": 1,
      "Accuracy": 1,
      "CycleTime": 1,
      "FalloffDistance": 1,
      "FalloffTracking": 1,
      "OptimalDistance": 1,
      "OptimalTracking": 1,
      "FalloffAimingCone": 1,
      "OptimalAimingCone": 1
    },
    "Velocity": {
      "Enabled": true,
      "BoosterEnabled": false,
      "BoosterAccelerationG": 5,
      "FarDistanceSu": 1.5,
      "TooCloseDistanceM": 15000,
      "BrakeDistanceFactor": 2,
      "OutsideOptimalRange": { "Negative": 0.25, "Positive": 1.2 },
      "OutsideOptimalRange2X": { "Negative": 0.5, "Positive": 1.5 },
      "InsideOptimalRange": { "Negative": 1, "Positive": 1 }
    }
  },
  "ServerProperties": {
    "Header": { "PrettyName": "Borg Cube" },
    "IsDynamicWreck": false
  }
}
```

