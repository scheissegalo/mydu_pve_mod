# Client (DuClient) warp destination – assembly findings

From `DuClient/assemblyInfos/` (client executable symbol index).

## Relevant client symbols

- **NQ::Game::WarpManager::setWarpPointConstructDestination**  
  The C++ API that sets the current warp destination. Called with:
  - `NQ::Net::RawMessageView` (when handling a network message), or
  - `NQ::PropertyValue` (single property value).

- **NQ::Game::WarpManager::initialize**  
  Registers handlers (e.g. for messages or property updates).  
  Refs: `game\game_warp.c:1856`, `by_address\text_140e01000.c` (36516, 36783, 36349, …).

- **FUN_140e07e10** @ **game\\game_constructs.c:144168**  
  Large function (1268b) that **calls setWarpPointConstructDestination**.  
  This is the main game-side path that applies a warp destination (from a message or from character state).

- **NQ::PlayerPropertyUpdate**  
  Used in network message handling (`by_address\text_140f81000.c`: 2118, 11093) and in  
  **NQ::Game::CharacterPropertiesComponent** (`by_address\text_140501000.c`: 34055, 34136, 3754).  
  So when the client receives a player property update (e.g. `PlayerPropertyUpdated` from the server), it goes through this component.

- **NQ::UI::MapsTab::setConstructIdAsWarpPoint** @ **game\\game_constructs.c:175621**  
  Map UI “set as warp destination” (right‑click). Exposed to HUD/CEF as `CPPMapsManager.setConstructIdAsWarpPoint`.

## Interpretation

1. The server correctly pushes the three warp properties (grain + Orleans HTTP SetDynamicProperty with `fromServer: true`), so the client receives **PlayerPropertyUpdated** (or equivalent) for `warpDestinationConstructId`, `warpDestinationConstructName`, `warpDestinationWorldPosition`.

2. The client has **PlayerPropertyUpdate** in the network layer and in **CharacterPropertiesComponent**, so incoming property updates are processed.

3. The open point is whether the handler for those updates, when it sees the warp destination property names, calls into **game_constructs.c:144168** (and thus **WarpManager::setWarpPointConstructDestination**). If it only updates an internal property store and the warp drive UI reads that store only at login, the UI would not refresh without relog.

4. The modinjectjs we send calls **CPPMapsManager.setConstructIdAsWarpPoint(beaconConstructId)**. If that only updates the map selection and does not call the same C++ path as **WarpManager::setWarpPointConstructDestination**, the warp drive widget (which may rely on the three player properties or on WarpManager state) would not update.

## What would fix live refresh

- **Option A (client fix):** In the client, ensure the **PlayerPropertyUpdated** (or equivalent) handler for `warpDestinationConstructId` / `warpDestinationConstructName` / `warpDestinationWorldPosition` calls the same path as **game_constructs.c:144168** (i.e. **WarpManager::setWarpPointConstructDestination** or the function at 144168). That would make server-pushed property updates refresh the warp UI without relog.

- **Option B (no client change):** If the client never refreshes warp from property updates, live update without relog is not possible without modifying the client binary.

## File references (assemblyInfos)

- `func_symbols.txt`: call graph (setWarpPointConstructDestination, PlayerPropertyUpdate, FUN_140e07e10).
- `master_symbols.txt`, `class_members.txt`: symbol → file/line.
- `lambda_chains.txt`: wrapper → real function (when resolved).
