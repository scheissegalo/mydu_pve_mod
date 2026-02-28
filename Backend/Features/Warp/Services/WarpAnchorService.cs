using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Backend;
using Backend.Database;
using Backend.Scenegraph;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mod.DynamicEncounters.Common.Helpers;
using Mod.DynamicEncounters.Database.Interfaces;
using Mod.DynamicEncounters.Features.Common.Data;
using Mod.DynamicEncounters.Features.Common.Interfaces;
using Mod.DynamicEncounters.Features.Common.Services;
using Mod.DynamicEncounters.Features.ExtendedProperties.Extensions;
using Mod.DynamicEncounters.Features.ExtendedProperties.Interfaces;
using Mod.DynamicEncounters.Features.Scripts.Actions.Data;
using Mod.DynamicEncounters.Features.TaskQueue.Interfaces;
using Mod.DynamicEncounters.Features.Warp.Data;
using Mod.DynamicEncounters.Features.Warp.Interfaces;
using Mod.DynamicEncounters.Helpers;
using Mod.DynamicEncounters.Features.NQ.Interfaces;
using Mod.DynamicEncounters.Vector.Helpers;
using Newtonsoft.Json;
using NQ;
using NQ.Interfaces;
using NQutils.Def;
using NQutils.Sql;
using Orleans;

namespace Mod.DynamicEncounters.Features.Warp.Services;

public class WarpAnchorService(IServiceProvider provider) : IWarpAnchorService
{
    private readonly ILogger<WarpAnchorService> _logger = provider.CreateLogger<WarpAnchorService>();
    private readonly IPlayerAlertService _playerAlertService = provider.GetRequiredService<IPlayerAlertService>();
    private readonly IAreaScanService _areaScanService = provider.GetRequiredService<IAreaScanService>();

    private readonly IClusterClient _orleans = provider.GetRequiredService<IClusterClient>();

    public async Task<CreateWarpAnchorOutcome> SpawnWarpAnchor(SpawnWarpAnchorCommand command)
    {
        if (string.IsNullOrEmpty(command.ElementTypeName))
        {
            return CreateWarpAnchorOutcome.InvalidElementTypeName();
        }

        var spawner = provider.GetRequiredService<IBlueprintSpawnerService>();
        var taskQueueService = provider.GetRequiredService<ITaskQueueService>();
        var traitRepository = provider.GetRequiredService<ITraitRepository>();
        var elementTraitMap = (await traitRepository.GetElementTraits(command.ElementTypeName)).Map();

        if (!elementTraitMap.TryGetValue("supercruise", out var trait))
        {
            return CreateWarpAnchorOutcome.ElementDoesNotHaveSuperCruise(command.ElementTypeName);
        }

        // Blueprint filename comes from trait property (mod_trait_properties.default_value or mod_element_trait_properties).
        // The default below is only used when the trait has no "blueprintFileName" property. If the DB still has the old name, override so the new file is used without a DB change.
        trait.TryGetPropertyValue("blueprintFileName", out var blueprintFileName, "PublicWarpBeacon.json");
        trait.TryGetPropertyValue("maxRange", out var maxRange, DistanceHelpers.OneSuInMeters * 100);

        var delta = command.TargetPosition - command.FromPosition;
        var distance = delta.Size();
        var direction = delta.NormalizeSafe();
        var beaconPosition = command.TargetPosition;
        var wasClamped = false;
        if (distance > maxRange)
        {
            beaconPosition = direction * maxRange + command.FromPosition;
            wasClamped = true;
        }

        try
        {
            var playerService = provider.GetRequiredService<IPlayerService>();
            var displayName = await playerService.FindPlayerNameById(command.PlayerId.id);
            var warpDestinationConstructName = "[!] " + (string.IsNullOrEmpty(displayName) ? "Unknown" : displayName) + " Warp";

            var constructId = await spawner.SpawnAsync(
                new SpawnArgs
                {
                    Folder = "pve",
                    File = blueprintFileName,
                    Position = beaconPosition,
                    IsUntargetable = true,
                    OwnerEntityId = new EntityId { playerId = command.PlayerId },
                    Name = warpDestinationConstructName
                }
            );

            var connectionFactory = provider.GetRequiredService<IPostgresConnectionFactory>();
            using var db = connectionFactory.Create();

            // Make sure the beacon is active by setting all elements to have been created 3 days in the past *shrugs*
            await db.ExecuteAsync(
                """
                UPDATE public.element SET created_at = NOW() - INTERVAL '3 DAYS' WHERE construct_id = @constructId
                """,
                new
                {
                    constructId = (long)constructId
                }
            );

            await taskQueueService.EnqueueScript(
                new ScriptActionItem
                {
                    Type = "reload-construct",
                    ConstructId = constructId
                },
                DateTime.UtcNow + TimeSpan.FromSeconds(60 + 50)
            );

            await taskQueueService.EnqueueScript(
                new ScriptActionItem
                {
                    Type = "delete",
                    ConstructId = constructId
                },
                DateTime.UtcNow + TimeSpan.FromMinutes(2)
            );

            var beaconPosString = beaconPosition.Vec3ToPosition();
            var playerIdLong = command.PlayerId.id;

            _logger.LogInformation("Warp anchor: updating player {PlayerId} warp destination via grain (UpdatePlayerProperty fromServer=true)", playerIdLong);

            // Update via IPlayerGrain.UpdatePlayerProperty(..., fromServer: true) so the grain calls NotifyPlayer(PlayerPropertyUpdated) and the client refreshes warp UI without relog.
            var playerGrain = _orleans.GetPlayerGrain(command.PlayerId);
            await playerGrain.UpdatePlayerProperty(new PlayerPropertyUpdate
            {
                playerId = playerIdLong,
                name = "warpDestinationConstructName",
                value = new PropertyValue(warpDestinationConstructName),
                relative = false
            }, fromServer: true);
            await playerGrain.UpdatePlayerProperty(new PlayerPropertyUpdate
            {
                playerId = playerIdLong,
                name = "warpDestinationConstructId",
                value = new PropertyValue(constructId),
                relative = false
            }, fromServer: true);
            await playerGrain.UpdatePlayerProperty(new PlayerPropertyUpdate
            {
                playerId = playerIdLong,
                name = "warpDestinationWorldPosition",
                value = new PropertyValue(beaconPosString),
                relative = false
            }, fromServer: true);

            _logger.LogInformation("Warp anchor: grain UpdatePlayerProperty completed for player {PlayerId}; pushing via Orleans HTTP as fallback", playerIdLong);

            // If Backend runs in a different process, the grain we called might not be the one with the client connection. Push via game server's Orleans HTTP API so the update runs where the client is connected.
            var httpOkCount = await TryPushWarpPropertiesViaOrleansHttp(playerIdLong, warpDestinationConstructName, constructId, beaconPosString);

            await NotifyPlayerWarpDestinationUpdated(command.PlayerId, constructId);

            // Always log at Warning so it appears in Mod.log; include HTTP result and known client limitation
            _logger.LogWarning(
                "Warp anchor created for player {PlayerId}, beacon {ConstructId}. Grain updates sent; Orleans HTTP SetDynamicProperty {HttpOk}/3. If warp drive UI does not update without relog, the game client likely does not refresh warp destination on PlayerPropertyUpdated.",
                playerIdLong,
                constructId,
                httpOkCount
            );

            return CreateWarpAnchorOutcome.WarpAnchorCreated(
                constructId,
                warpDestinationConstructName,
                beaconPosition,
                wasClamped
                    ? $"Beacon created as far as possible ({maxRange / (double)DistanceHelpers.OneSuInMeters:F0} SU limit in game)."
                    : "Beacon created at the given position."
            );
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to create warp anchor");
            return CreateWarpAnchorOutcome.Failed("Failed to create warp anchor", e);
        }
    }

    /// <summary>
    /// Notifies the player that warp destination was set. Properties are updated via IPlayerGrain.UpdatePlayerProperty(..., fromServer: true)
    /// so the grain calls NotifyPlayer(PlayerPropertyUpdated); we also push via the game server's Orleans HTTP API (SetDynamicProperty)
    /// in case the Backend runs in a different process. We also send a fallback JS script (modinjectjs) to set the warp point.
    /// </summary>
    private async Task NotifyPlayerWarpDestinationUpdated(PlayerId playerId, ulong beaconConstructId)
    {
        await _playerAlertService.SendInfoAlert(playerId, "Warp destination set. You can use your warp drive to travel there.");

        // Try to refresh warp destination on client: set warp point (map API), retry if CPPMapsManager not loaded yet, trigger engine event.
        var refreshScript = "var __warpBeaconId=" + beaconConstructId + ";"
            + "function __applyWarpPoint(){ try { if (typeof CPPMapsManager !== 'undefined' && typeof CPPMapsManager.setConstructIdAsWarpPoint === 'function') { CPPMapsManager.setConstructIdAsWarpPoint(__warpBeaconId); return true; } } catch(e) {} return false; }"
            + "if (!__applyWarpPoint()) { setTimeout(__applyWarpPoint, 2000); setTimeout(__applyWarpPoint, 5000); }"
            + "if (typeof engine !== 'undefined' && typeof engine.trigger === 'function') { try { engine.trigger('WidgetStack.RequestUpdate'); } catch(e) {} }"
            + "if (typeof CPPHud !== 'undefined' && CPPHud.addSimpleNotification) CPPHud.addSimpleNotification('Warp destination refresh sent.');";

        // Best-effort push: send JS immediately to the player's client via the Orleans HTTP meta route.
        var sent = await TrySendModTriggerHudEventViaOrleansHttp(playerId, "modinjectjs", refreshScript);
        if (!sent)
        {
            var store = provider.GetRequiredService<IWarpDestinationRefreshStore>();
            store.SetPendingScript(playerId, refreshScript);
        }
        else
        {
            _logger.LogInformation("Warp destination refresh script sent to player {PlayerId} via Orleans metamessage", playerId);
        }
    }

    private async Task<bool> TrySendModTriggerHudEventViaOrleansHttp(ulong playerId, string eventName, string eventPayload)
    {
        try
        {
            var orleansHttpUrl = Environment.GetEnvironmentVariable("ORLEANS_HTTP_URL") ?? "http://orleans:10111";

            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            using var httpClient = httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Remove("Accept");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            var innerPayload = new
            {
                eventName,
                eventPayload
            };
            var serializedInnerPayload = JsonConvert.SerializeObject(innerPayload);

            var payload = new
            {
                targetType = "player",
                targetId = playerId,
                requestName = "ModTriggerHudEventRequest",
                serializedPayload = serializedInnerPayload
            };

            var jsonPayload = JsonConvert.SerializeObject(payload);
            using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"{orleansHttpUrl.TrimEnd('/')}/meta/metamessage", content);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Orleans HTTP metamessage sent for player {PlayerId} to {Url}", playerId, $"{orleansHttpUrl.TrimEnd('/')}/meta/metamessage");
                return true;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogWarning(
                "Orleans HTTP metamessage failed for player {PlayerId}: HTTP {Status} {Body}",
                playerId,
                response.StatusCode,
                responseBody
            );
            return false;
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Orleans HTTP metamessage send failed for player {PlayerId}", playerId);
            return false;
        }
    }

    /// <summary>
    /// Pushes the three warp destination properties to the game server via its Orleans HTTP API
    /// (Player/{playerId}/setdynamicplayerproperty/true). Use when the Backend runs in a different process
    /// so the grain we call might not be the silo that has the client connection; the HTTP request is
    /// handled by the game server and NotifyPlayer(PlayerPropertyUpdated) runs where the client is connected.
    /// Returns the number of successful HTTP calls (0–3).
    /// </summary>
    private async Task<int> TryPushWarpPropertiesViaOrleansHttp(ulong playerId, string warpDestinationConstructName, ulong constructId, string beaconPosString)
    {
        var orleansHttpUrl = Environment.GetEnvironmentVariable("ORLEANS_HTTP_URL") ?? "http://orleans:10111";
        var baseUrl = orleansHttpUrl.TrimEnd('/');
        var url = $"{baseUrl}/Player/{playerId}/setdynamicplayerproperty/true";
        _logger.LogInformation("Warp anchor: pushing 3 properties via Orleans HTTP for player {PlayerId} to {Url}", playerId, url);

        try
        {
            var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
            using var httpClient = httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Remove("Accept");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            // PropertyValueJsonConverter expects value: { "type": PropertyType, "value": <primitive> }. PropertyType: INTEGER=2, STRING=4.
            var ok = 0;
            var payload1 = new { playerId, name = "warpDestinationConstructName", value = new { type = 4, value = warpDestinationConstructName }, relative = false };
            using (var c1 = new StringContent(JsonConvert.SerializeObject(payload1), Encoding.UTF8, "application/json"))
            {
                var r1 = await httpClient.PostAsync(url, c1);
                if (r1.IsSuccessStatusCode) ok++; else _logger.LogWarning("Orleans HTTP SetDynamicProperty failed for player {PlayerId} warpDestinationConstructName: HTTP {Status} {Body}", playerId, r1.StatusCode, await r1.Content.ReadAsStringAsync());
            }
            var payload2 = new { playerId, name = "warpDestinationConstructId", value = new { type = 2, value = (long)constructId }, relative = false };
            using (var c2 = new StringContent(JsonConvert.SerializeObject(payload2), Encoding.UTF8, "application/json"))
            {
                var r2 = await httpClient.PostAsync(url, c2);
                if (r2.IsSuccessStatusCode) ok++; else _logger.LogWarning("Orleans HTTP SetDynamicProperty failed for player {PlayerId} warpDestinationConstructId: HTTP {Status} {Body}", playerId, r2.StatusCode, await r2.Content.ReadAsStringAsync());
            }
            var payload3 = new { playerId, name = "warpDestinationWorldPosition", value = new { type = 4, value = beaconPosString }, relative = false };
            using (var c3 = new StringContent(JsonConvert.SerializeObject(payload3), Encoding.UTF8, "application/json"))
            {
                var r3 = await httpClient.PostAsync(url, c3);
                if (r3.IsSuccessStatusCode) ok++; else _logger.LogWarning("Orleans HTTP SetDynamicProperty failed for player {PlayerId} warpDestinationWorldPosition: HTTP {Status} {Body}", playerId, r3.StatusCode, await r3.Content.ReadAsStringAsync());
            }
            _logger.LogInformation("Warp anchor: Orleans HTTP SetDynamicProperty completed for player {PlayerId}: {Ok}/3 ok", playerId, ok);
            return ok;
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Orleans HTTP SetDynamicProperty failed for player {PlayerId}", playerId);
            return 0;
        }
    }

    public async Task<CreateWarpAnchorOutcome> CreateWarpAnchorForPosition(CreateWarpAnchorCommand command)
    {
        const string pWarpAnchorTimePoint = "warpAnchorTimePoint";
        var playerId = command.PlayerId;

        var sql = provider.GetRequiredService<ISql>();
        var bank = provider.GetRequiredService<IGameplayBank>();
        var sceneGraph = provider.GetRequiredService<IScenegraph>();
        
        var propVal = await sql.ReadPlayerProperty_Generic(playerId, pWarpAnchorTimePoint);
        if (propVal is { value: not null })
        {
            var timePoint = new TimePoint { networkTime = propVal.intValue };
            var nowTimePoint = TimePoint.Now();

            var timeSpan = nowTimePoint.ToDateTime() - timePoint.ToDateTime();

            if (timeSpan < TimeSpan.FromMinutes(3))
            {
                var cooldownTime = timePoint.ToDateTime() + TimeSpan.FromMinutes(3);
                var remaining = nowTimePoint.ToDateTime() - cooldownTime;

                return CreateWarpAnchorOutcome.OnCooldown(remaining);
            }
        }

        var playerLocalPosition = await sceneGraph.GetPlayerLocalPosition(command.PlayerId);
        if (playerLocalPosition == null)
        {
            return CreateWarpAnchorOutcome.InvalidPlayerPosition();
        }

        var constructId = playerLocalPosition.constructId;
        var constructPos = await sceneGraph.GetConstructCenterWorldPosition(constructId);

        var constructGrain = _orleans.GetConstructGrain(constructId);
        var constructElementGrain = _orleans.GetConstructElementsGrain(constructId);

        var pilotId = await constructGrain.GetPilot();

        if (pilotId == null)
        {
            return CreateWarpAnchorOutcome.MustBePilotingConstruct();
        }

        var position = command.TargetPosition ?? new Vec3();

        if (!command.TargetPosition.HasValue)
        {
            var waypointPosString = await sql.ReadPlayerProperty(playerId, Character.d_currentWaypoint);

            if (waypointPosString == null || !waypointPosString.StartsWith("::pos{0,0"))
            {
                return CreateWarpAnchorOutcome.InvalidWaypoint();
            }

            _logger.LogInformation("Found Waypoint: {WP}", waypointPosString);

            try
            {
                position = waypointPosString.PositionToVec3();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Invalid Waypoint: {WP}", waypointPosString);
                return CreateWarpAnchorOutcome.InvalidWaypoint();
            }
        }

        var direction = (position - constructPos).Normalized();
        var offsetPos = direction * command.Offset;

        position += offsetPos;
        
        var contacts = await _areaScanService.ScanForPlanetaryBodies(position, 0.25d * DistanceHelpers.OneSuInMeters);
        if (contacts.Any())
        {
            return CreateWarpAnchorOutcome.TooCloseToAPlanet();
        }

        var warpDrives = await constructElementGrain.GetElementsOfType<WarpDriveUnit>();
        if (warpDrives.Count == 0)
        {
            return CreateWarpAnchorOutcome.MissingDriveUnit();
        }

        var driveUnitElementId = warpDrives.First();
        var driveUnitElementInfo = await constructElementGrain.GetElement(driveUnitElementId);
        var driveDef = bank.GetDefinition(driveUnitElementInfo.elementType);

        if (driveDef == null)
        {
            return CreateWarpAnchorOutcome.InvalidDriveUnit();
        }

        try
        {
            if (EnvironmentVariableHelper.IsProduction())
            {
                var propValue = await sql.ReadPlayerProperty_Generic(playerId, pWarpAnchorTimePoint);
                if (propValue?.value == null)
                {
                    await sql.SetPlayerProperties(playerId, new Dictionary<string, PropertyValue>
                    {
                        { pWarpAnchorTimePoint, new PropertyValue(TimePoint.Now().networkTime) }
                    });
                }
                else
                {
                    await sql.UpdatePlayerProperty_Generic(
                        playerId,
                        pWarpAnchorTimePoint,
                        new PropertyValue(TimePoint.Now().networkTime)
                    );
                }
            }
        }
        catch (Exception e)
        {
            await _playerAlertService.SendErrorAlert(
                playerId,
                "Failed to update warp anchor timer"
            );

            _logger.LogError(e, "Failed to update warp anchor timer");
        }

        try
        {
            return await SpawnWarpAnchor(
                new SpawnWarpAnchorCommand
                {
                    PlayerId = command.PlayerId,
                    FromPosition = constructPos,
                    TargetPosition = position,
                    ElementTypeName = driveDef.Name
                });
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failure to Create Warp Anchor");

            return CreateWarpAnchorOutcome.Failed("Failure to Create Warp Anchor", e);
        }
    }

    public async Task<CreateWarpAnchorOutcome> CreateWarpAnchorForward(CreateWarpAnchorForwardCommand command)
    {
        var constructService = provider.GetRequiredService<IConstructService>();
        var sceneGraph = provider.GetRequiredService<IScenegraph>();

        var playerLocalPosition = await sceneGraph.GetPlayerLocalPosition(command.PlayerId);
        if (playerLocalPosition == null)
        {
            return CreateWarpAnchorOutcome.InvalidPlayerPosition();
        }

        var constructId = playerLocalPosition.constructId;

        var info = await constructService.GetConstructInfoAsync(constructId);
        var quat = info.Info!.rData.rotation.ToQuat();
        var pos = await sceneGraph.GetConstructCenterWorldPosition(constructId);

        var forward = Vector3.Transform(Vector3.UnitY, quat);
        var aheadPos = pos + forward.ToNqVec3() * command.Distance * DistanceHelpers.OneSuInMeters;

        return await CreateWarpAnchorForPosition(
            new CreateWarpAnchorCommand
            {
                PlayerId = command.PlayerId,
                TargetPosition = aheadPos,
            }
        );
    }

    public async Task<SetWarpCooldownOutcome> SetWarpCooldown(SetWarpCooldownCommand command)
    {
        var traitRepository = provider.GetRequiredService<ITraitRepository>();
        var elementTraitMap = (await traitRepository.GetElementTraits(command.ElementTypeName)).Map();

        if (!elementTraitMap.TryGetValue("supercruise", out var trait))
        {
            return SetWarpCooldownOutcome.NotASupercruiseDrive(command.ElementTypeName);
        }

        trait.TryGetPropertyValue("warpEndCooldown", out var warpEndCooldown, TimeSpan.FromSeconds(3).TotalMilliseconds);

        // Trait stores cooldown in milliseconds. Cap at 300 seconds (game's usual max) so distance-based or wrong values don't yield huge cooldowns.
        const double maxCooldownSeconds = 300;
        var cooldownMs = warpEndCooldown;
        if (cooldownMs > maxCooldownSeconds * 1000)
            cooldownMs = maxCooldownSeconds * 1000;

        var cooldownDate = DateTime.UtcNow + TimeSpan.FromMilliseconds(cooldownMs);
        
        var orleans = provider.GetOrleans();
        var constructElementsGrain = orleans.GetConstructElementsGrain(command.ConstructId);
        var coreUnits = await constructElementsGrain.GetElementsOfType<CoreUnit>();

        if (coreUnits.Count == 0)
        {
            return SetWarpCooldownOutcome.InvalidConstruct();
        }

        await constructElementsGrain.UpdateElementProperty(new ElementPropertyUpdate
        {
            constructId = command.ConstructId,
            name = "endOfWarpCooldown",
            elementId = coreUnits.First().elementId,
            value = new PropertyValue(cooldownDate.ToNQTimePoint().networkTime),
            timePoint = TimePoint.Now()
        });

        return SetWarpCooldownOutcome.CooldownSet();
    }
}