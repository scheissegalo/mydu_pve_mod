# Quick Start via APIs

- Create a bot account with the roles `Game` and `bot`

## Docker Compose - if running on docker

Run the mod with extra environment variables:

```yaml
mod_dynamic_encounters:
  image: voidrunner7891/dynamic_encounters:latest
  ports:
    - "8080:8080"
  environment:
    BOT_LOGIN: ${PVE_BOT_USERNAME} # your bot username
    BOT_PASSWORD: ${PVE_BOT_PASSWORD} # your bot password
    BOT_PREFIX: ${PVE_BOT_PREFIX} # your bot ingame name
    API_ENABLED: "true" # Enable APIs
    CORS_ALLOW_ALL: "true" # Enables CORS. Consider security by allowing requests to come from any origin with this enabled. Adjust the domains of the container for optimal settings and disable cors.
  volumes:
    - ${DATAPATH}:/data
    - ${LOGPATH}:/logs
    - ${CONFPATH}:/config
  networks:
    vpcbr:
      ipv4_address: 10.5.0.21
```

The port will be `8080`

## Locally from the Code

If you're running locally, make sure to set the environment variables:

- API_ENABLED=true
- CORS_ALLOW_ALL=true

The port will be `5000`

## Access the Swagger

### Locally

[http://localhost:5000/swagger/](http://localhost:5000/swagger/)

### Running on Docker

[http://mod_dynamic_encounters:8080/swagger](http://mod_dynamic_encounters:8080/swagger) or [http://localhost:8080/swagger](http://localhost:8080/swagger)

### Setting up Data

You can also hit those endpoints using a tool like postman.

## Understanding the System Architecture

The mod works in layers:

1. **Prefabs** - Blueprint definitions for constructs (ships, wrecks, POIs)
2. **Scripts** - Actions that spawn prefabs and control behavior
3. **Sector Encounters** - Templates that define what can spawn in sectors
4. **Sector Instances** - Actual spawned sectors in the game world
5. **Factions & Territories** - Control where encounters can spawn

### How It Works:

1. **Prefabs** define what constructs look like (blueprints)
2. **Scripts** define what happens (spawn prefab X, do action Y)
3. **Sector Encounters** link scripts to potential spawn locations:
   - `onLoadScript` - Runs when sector is first loaded (spawns POI/wreck marker)
   - `onSectorEnterScript` - Runs when player enters sector (spawns NPC/encounter)
4. **Sector Instances** are actual sectors that get generated from encounters
5. When a player enters a sector, the `onSectorEnterScript` runs and spawns the encounter

## Step-by-Step Setup

### Step 1: Install Starter Content

**POST** `/starter`

This endpoint:

- Creates prefab definitions for "basic-poi" and "basic-pirate"
- Creates scripts: `spawn-basic-poi`, `spawn-basic-pirate`, `expire-sector-default`
- Creates one sector encounter: "Basic Pirate Encounter"
- Enables required features
- **Does NOT create sectors yet** - that happens automatically later

**What you get:**

- A prefab for a simple POI (Point of Interest)
- A prefab for a basic pirate ship
- Scripts to spawn them
- One sector encounter template

### Step 2: Understanding Wreck vs NPC vs Sector Encounter Endpoints

#### `/wreck` and `/npc` Endpoints (Optional)

These create **new prefabs and scripts** for custom wrecks/NPCs. You only need these if you want to add your own custom constructs beyond the starter content.

- **`POST /wreck`** - Creates a wreck prefab + spawn script
- **`POST /npc`** - Creates an NPC prefab + spawn script

**When to use:**

- You have custom blueprint files you want to add
- You want wrecks/NPCs not in starter content
- **You DON'T need this if starter content is enough**

#### `/sector/encounter` Endpoints (Required)

These add **encounter templates** to the pool. These define what can spawn when sectors are generated.

- **`PUT /sector/encounter/wreck?WreckScript=<script-name>`** - Adds a wreck encounter
- **`PUT /sector/encounter/npc?POIScript=<poi-script>&NpcScript=<npc-script>`** - Adds an NPC encounter

**What they do:**

- Add entries to `mod_sector_encounter` table
- These become options when sectors are generated
- Each encounter needs a territory (default territory is created automatically)

### Step 3: Check What You Have

**GET** `/sector/encounter` - Lists all sector encounters

You should see entries like:

- "Basic Pirate Encounter" (from starter content)
- Any wrecks/NPCs you added via sector encounter endpoints

Each encounter has:

- `onLoadScript` - Script that runs when sector loads (usually spawns POI marker)
- `onSectorEnterScript` - Script that runs when player enters (spawns the actual encounter)
- `territoryId` - Which territory this encounter belongs to
- `properties` - Spawn radius, center position, expiration times

### Step 4: Verify Sectors Are Being Generated

**GET** `/sector/instance` - Lists all sector instances (actual spawned sectors)

**GET** `/sector/instance/active` - Lists active sectors (where encounters have triggered)

**What to check:**

- If `/sector/instance` returns empty: Sectors haven't been generated yet
- Sectors are generated automatically by background workers
- They're generated for each faction-territory combination
- Default faction (id=1, "Pirates") and default territory are created automatically

### Step 5: Force Sector Regeneration (If Needed)

**POST** `/sector/instance/expire/force/all` - Forces all sectors to expire and regenerate

This will:

- Delete all existing sector instances
- Trigger regeneration of new sectors
- New sectors will randomly pick from your active encounters

### Step 6: Restart the Service

**Important:** Scripts are cached in memory. After adding new scripts or encounters, restart:

```bash
docker-compose restart mod_dynamic_encounters
```

## Troubleshooting

### "I see encounters but no sectors are spawning"

1. **Check if sectors exist:**

   ```bash
   curl http://localhost:5480/sector/instance
   ```

2. **Check if encounters have territories:**

   ```bash
   curl http://localhost:5480/sector/encounter
   ```

   Each encounter should have a `territoryId`. If they're all the same default territory, that's fine.

3. **Force regeneration:**

   ```bash
   curl -X POST http://localhost:5480/sector/instance/expire/force/all
   ```

4. **Check logs** for sector generation messages:
   ```bash
   docker-compose logs -f mod_dynamic_encounters | grep -i sector
   ```

### "Encounters aren't triggering when I enter sectors"

1. **Check if sector is active:**

   ```bash
   curl http://localhost:5480/sector/instance/active
   ```

2. **Manually activate a sector:**

   ```bash
   curl -X POST http://localhost:5480/sector/instance/activate \
     -H "Content-Type: application/json" \
     -d '{"sector": {"x": 13771471, "y": 7435803, "z": -128971}}'
   ```

3. **Verify scripts exist:**
   ```bash
   curl http://localhost:5480/script
   ```

### "How do I see what constructs are spawned?"

The mod doesn't have a direct API for this, but you can:

1. **Check game logs** for spawn messages
2. **Use the game's construct browser** - spawned constructs will appear
3. **Check sector instances** - active sectors have `startedAt` timestamp set

### Understanding the Flow

```
1. Starter Content creates:
   ├─ Prefabs (basic-poi, basic-pirate)
   ├─ Scripts (spawn-basic-poi, spawn-basic-pirate)
   └─ Sector Encounter (links scripts together)

2. Background Worker (SectorSpawnerWorker):
   ├─ Checks each faction-territory
   ├─ Finds active encounters for that territory
   └─ Generates sector instances (actual sectors in game)

3. When Player Enters Sector:
   ├─ Sector Activation Worker detects player
   ├─ Runs onSectorEnterScript
   └─ Spawns the encounter (NPC/wreck)

4. When Sector Expires:
   └─ Sector is cleaned up and can be regenerated
```

## Quick Reference

| Endpoint                                 | Purpose             | When to Use              |
| ---------------------------------------- | ------------------- | ------------------------ |
| `POST /starter`                          | Initial setup       | First time setup only    |
| `GET /sector/encounter`                  | List encounters     | Check what's configured  |
| `PUT /sector/encounter/wreck`            | Add wreck encounter | Add wrecks to spawn pool |
| `PUT /sector/encounter/npc`              | Add NPC encounter   | Add NPCs to spawn pool   |
| `GET /sector/instance`                   | List all sectors    | See generated sectors    |
| `GET /sector/instance/active`            | List active sectors | See triggered encounters |
| `POST /sector/instance/expire/force/all` | Regenerate all      | Force new sectors        |
| `GET /script`                            | List scripts        | See available scripts    |
| `GET /prefab`                            | List prefabs        | See available constructs |
