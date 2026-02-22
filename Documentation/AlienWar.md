# Alien War Event API

The Alien War feature lets you start an event at an alien core construct. Spawned enemies will attack the core (shield or hull), switch to guard mode during shield lockdown, and the event ends when either all spawns are destroyed (players win) or the core is destroyed. When the bots destroy the core, the mod **claims** the core (changes owner to the bot player) and **repairs** all elements (including the shield) to full (`hitpointsRatio` = 1.0), then despawns the bots and ends the event.

## Endpoints

### POST /alienwar/start

Starts an Alien War event at the given alien core. Runs the specified spawn script at the given sector; spawned constructs are tagged `alienwar` and target the core.

**Request body:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| constructId | number | Yes | Alien core construct ID (e.g. 990003). |
| sector | object | Yes | Sector coordinates `{ "x", "y", "z" }`. |
| scriptName | string | Yes | Name of the script in `mod_script` to run (must spawn constructs with tag `alienwar`). |
| cooldownSecondsOverride | number | No | Override lockdown cooldown for testing; if set, used instead of DB `lockdownEnd`. |

**Response:** `200 OK` with `{ "eventStarted": true, "coreConstructId": 990003 }`.

**Errors:** `404` if construct not found or deleted; `400` if script execution fails.

### GET /alienwar/overview

Returns **overall Alien War status**: how many active events and, per event, core construct ID, sector, script name, phase, and alive bot count.

**Response:** `200 OK` with:

```json
{
  "activeEventCount": 2,
  "events": [
    {
      "coreConstructId": 990003,
      "sector": { "x": 0, "y": 0, "z": 0 },
      "scriptName": "alienwar-spawn-hard-pirate",
      "phase": "Attack",
      "botCount": 3,
      "createdAt": "2025-02-22T12:00:00Z"
    }
  ]
}
```

- **activeEventCount**: Number of active events (rows in `mod_alien_war_event`).
- **events**: One object per active event: **coreConstructId**, **sector**, **scriptName**, **phase** (from in-memory state), **botCount** (alive bots in that event’s sector for that core), **createdAt**.

**Errors:** None; returns `activeEventCount: 0` and `events: []` when there are no active events.

### GET /alienwar/status/{constructId}

Returns the current phase, in-memory state, and **live shield status** (same data the check task and bots use) for debugging.

**Response:** `200 OK` with:

```json
{
  "phase": "Attack",
  "lockdownEndAtUtc": "2025-02-22T14:00:00Z",
  "bots": {
    "aliveCount": 3,
    "targetingCore": 1,
    "targetingPlayers": 2,
    "targetingPlayerConstructIds": [ 12345, 67890 ]
  },
  "shield": {
    "shieldEnabled": true,
    "lockdownExitAtUtc": "2025-02-22T14:00:00Z",
    "isInLockdown": true,
    "shieldHealthPercent": 90.5
  }
}
```

- `phase`: In-memory phase the bots use (`Attack`, `Guard`, or `Ended`).
- `lockdownEndAtUtc`: Value last written by the check task (may be null if no lockdown was seen yet).
- `bots`: **aliveCount** = number of alien-war bots still in sector; **targetingCore** = bots with target = core (from actual target data); **targetingPlayers** = bots with target = a player construct (from actual target data); **targetingPlayerConstructIds** = distinct construct IDs currently targeted. Counts are from actual target data only. In Attack phase: when no players are within 400 km of the core, all bots target the core; when players are in range, one bot (min ConstructId) targets the core and the rest target players (with a 400 km leash from the core).
- `shield`: Live read from DB. **shieldEnabled**, **lockdownExitAtUtc**, **isInLockdown** as before. **shieldHealthPercent** = 0–100% from `(1 - totalDamage/shieldMaxHp)`, using `hitHistory.totalDamage` and `shieldMaxHp` (null if not available). If shield could not be read, `shield` is `{ "error": "..." }`.

**Errors:** `404` if no active event for this construct.

### POST /alienwar/cancel/{constructId}

Cancels an active Alien War event manually (for testing): despawns all alien-war bots in that event’s sector and removes the event from `mod_alien_war_event`. Same effect as when the event ends (core destroyed or all spawns dead).

**Response:** `200 OK` with `{ "cancelled": true, "coreConstructId": 990003 }`.

**Errors:** `404` if no active event for this construct.

### POST /alienwar/claim/{constructId}

Sets the construct’s owner (claim). Useful to force-claim a core or any construct without running the full “core destroyed” flow.

**Request body (optional):** `{ "playerId": 123 }`. If omitted, the bot player is used as owner.

**Response:** `200 OK` with `{ "claimed": true, "constructId": 990003, "ownerPlayerId": 123 }`.

**Errors:** `404` if construct not found or deleted.

### POST /alienwar/repair/{constructId}

Repairs all elements on the construct by setting `hitpointsRatio` = 1.0 for each element (including shield). Useful to restore a core or any construct to full HP without running the full “core destroyed” flow.

**Response:** `200 OK` with `{ "repaired": true, "constructId": 990003, "elementCount": 42 }`.

**Errors:** `404` if construct not found or deleted.

**Note:** When the core is destroyed by the bots, the next `alienwar-check` run will: (1) set the core’s owner to the bot player (`ConstructSetOwner`), (2) repair every element on the construct to full HP (`UpdateElementProperty` with `hitpointsRatio` = 1.0 for each element), then (3) despawn all alien-war bots and end the event. The core remains in the world as a bot-owned, fully repaired construct.

The sector force-expire endpoint `POST /sector/instance/expire/force/all` does **not** affect Alien War events. It only sets `force_expire_at = NOW()` on all rows in `mod_sector_instance`, so sector instances expire; Alien War state lives in `mod_alien_war_event` and is independent. Use `POST /alienwar/cancel/{constructId}` to cancel a specific event.

---

## Curl examples

**Start event:**

```bash
curl -X POST http://localhost:5000/alienwar/start \
  -H "Content-Type: application/json" \
  -d '{
    "constructId": 990003,
    "sector": { "x": 0, "y": 0, "z": 0 },
    "scriptName": "alienwar-spawn-hard-pirate"
  }'
```

**Start with cooldown override (testing):**

```bash
curl -X POST http://localhost:5000/alienwar/start \
  -H "Content-Type: application/json" \
  -d '{
    "constructId": 990003,
    "sector": { "x": 0, "y": 0, "z": 0 },
    "scriptName": "alienwar-spawn-hard-pirate",
    "cooldownSecondsOverride": 300
  }'
```

**Get status:**

```bash
curl http://localhost:5000/alienwar/status/990003
```

**Cancel event (testing):**

```bash
curl -X POST http://localhost:5000/alienwar/cancel/990003
```

---

## mod_script example for Alien War

Create a script in `mod_script` that spawns enemies with the tag `alienwar`. The API injects `AlienWarTargetConstructId` into the script context, and `SpawnScriptAction` passes `context.Properties` to each construct handle. In **Attack** phase, when no player constructs are within 400 km of the core, all ships target the core; when players are in range, one ship (smallest construct ID) stays on the core and the rest hunt players (with a 400 km leash from the core). In **Guard** phase, all ships target only player constructs (core is excluded).

Example script name: `alienwar-spawn-hard-pirate`. Insert into `mod_script`:

```json
{
  "Name": "alienwar-spawn-hard-pirate",
  "Area": {
    "Type": "sphere",
    "Height": 200000,
    "Radius": 1,
    "Rotation": { "W": 1, "X": 0, "Y": 0, "Z": 0 },
    "MinRadius": 100000
  },
  "Sector": { "x": 0, "y": 0, "z": 0 },
  "Actions": [
    {
      "Type": "spawn",
      "Prefab": "basic-pirate",
      "Tags": ["alienwar", "ore-1-2", "parts-1-2"],
      "MinQuantity": 1,
      "MaxQuantity": 3,
      "Area": { "Type": "sphere", "Radius": 100000 },
      "Events": {
        "OnLoad": [
          { "Type": "spawn-loot", "Tags": ["ore-1-2", "parts-1-2"], "Value": 2000 }
        ],
        "OnSectorEnter": []
      }
    },
    {
      "Type": "spawn",
      "Prefab": "hard-pirate-m",
      "Tags": ["alienwar", "ore-2-3", "ore-4-5"],
      "MinQuantity": 1,
      "MaxQuantity": 2,
      "Area": { "Type": "sphere", "Radius": 100000 },
      "Events": {
        "OnLoad": [
          { "Type": "spawn-loot", "Tags": ["ore-2-3", "ore-4-5"], "Value": 10000 }
        ],
        "OnSectorEnter": []
      }
    },
    {
      "Type": "spawn",
      "Prefab": "extreme-pirate",
      "Tags": ["alienwar", "ore-2-3", "ore-4-5", "plasma"],
      "MinQuantity": 1,
      "MaxQuantity": 1,
      "Area": { "Type": "sphere", "Radius": 100000 },
      "Events": {
        "OnLoad": [
          { "Type": "spawn-loot", "Tags": ["ore-2-3", "ore-4-5", "plasma"], "Value": 20000 }
        ],
        "OnSectorEnter": []
      }
    }
  ]
}
```

**Important:** Each spawn action must include `"alienwar"` in `Tags`. The API sets `AlienWarTargetConstructId` in the script context before execution, so you do not need to set it in the script; it is passed to construct handles automatically.

---

## Event flow

1. **Start:** You call `POST /alienwar/start` with `constructId`, `sector`, and `scriptName`. The mod triggers voxel cache for the core (so shooting can resolve hit points), runs the script at that sector, stores the event in `mod_alien_war_event`, and enqueues a periodic `alienwar-check` task.
2. **Attack:** By default **all** ships target the alien core. When a player construct is detected within **400 km of the core**, the mod switches to: one ship (lowest construct ID) keeps shooting the core, the rest hunt that player. When the player is destroyed or leaves the 400 km range, all ships return to shooting the core. Bots never chase players beyond 400 km from the core (leash). The mod reads `construct.shield_enabled` and, if enabled, the base shield element (type 1430252067) and its `element_property` **lockdownEnd**. When `lockdownEnd` is present with a value, the shield is in lockdown until that time (value = Unix time in **milliseconds**, UTC).
3. **Lockdown (Guard):** When the core’s shield is in lockdown (`lockdownEnd` set and current time &lt; that value), the phase switches to **Guard**. All NPCs stop shooting the core and target only player constructs in the area.
4. **End:** The event ends when (a) the core is destroyed, or (b) all spawned enemies are destroyed. Remaining spawns are despawned, the row in `mod_alien_war_event` is removed, and in-memory state is cleared.

## Testing: simulating lockdown ended

The mod reads **lockdownEnd** from the **game database** (`element_property` on the shield element, same DB as the game). The value is **Unix time in milliseconds (UTC)**. We compare `DateTime.UtcNow < LockdownExitAtUtc` to decide Guard vs Attack, so everything is UTC and there is no timezone mix.

To simulate "cooldown passed" so the phase switches to Attack:

1. Set **lockdownEnd** to a time **in the past** (e.g. now − 1 minute) as Unix **milliseconds**.
2. The next **alienwar-check** run (about every 30 seconds) will re-read the shield, see `UtcNow >= lockdownEnd`, set phase to Attack, and bots will target the core again.

Example (PostgreSQL): to set lockdown end to “1 minute ago” for the shield element of construct `990003`:

```sql
UPDATE element_property ep
SET value = (encode(convert_to(((extract(epoch from now() - interval '1 minute') * 1000)::bigint::text), 'UTF8'), 'escape'))
FROM element e
WHERE e.id = ep.element_id
  AND e.construct_id = 990003
  AND e.element_type_id = 1430252067
  AND ep.name = 'lockdownEnd';
```

The mod accepts the value as either an ASCII string or 8-byte binary. If the status still shows a **future** `lockdownExitAtUtc` after you change it, the game may have overwritten the row (e.g. on sector load or shield tick), or a different `element_id` was updated. We read the same game DB and compare with `DateTime.UtcNow` (no timezone conversion).

## State and restart

- **Active events** are stored in **`mod_alien_war_event`** (one row per core: `core_construct_id`, sector, `script_name`, optional `cooldown_seconds_override`). This is written when you start an event and deleted when the event ends.
- **Spawned bots** are normal construct handles in **`mod_npc_construct_handle`** with tag `alienwar` and context `AlienWarTargetConstructId`. They are not stored in a separate “event” table; the mod finds them by tag and context.
- **On mod restart:** When the task queue worker runs, it calls `ResumeAlienWarEventsIfNeededAsync()` once. That loads all rows from `mod_alien_war_event` and enqueues one `alienwar-check` task per event (delivery = now). Those tasks run, re-read shield state and phase, update in-memory state, and continue scheduling further checks. So after a restart, existing spawns and the core are still in the game; the mod resumes polling and the bots (which get their phase from `AlienWarStateService`) again behave correctly (one on core, others on players in Attack; all on players in Guard).
