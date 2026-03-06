using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Mod.DynamicEncounters.Features.Warp.Data;
using Mod.DynamicEncounters.Features.Warp.Interfaces;
using Mod.DynamicEncounters.Vector.Helpers;
using Newtonsoft.Json;
using NQ;

namespace Mod.DynamicEncounters.Api.Controllers;

[Route("warp")]
public class WarpController : Controller
{
    [HttpPost]
    [Route("anchor/v2")]
    public async Task<IActionResult> CreateWarpAnchorV2([FromBody] WarpAnchorRequestV2 request)
    {
        if (request.PlayerId == default)
        {
            return BadRequest();
        }

        var provider = ModBase.ServiceProvider;
        var warpAnchorService = provider.GetRequiredService<IWarpAnchorService>();

        var outcome = await warpAnchorService.SpawnWarpAnchor(
            new SpawnWarpAnchorCommand
            {
                FromPosition = request.FromPosition,
                TargetPosition = request.TargetPosition,
                ElementTypeName = request.ElementTypeName,
                PlayerId = request.PlayerId,
                Public = request.Public,
                DespawnMinutes = request.DespawnMinutes,
                Name = request.Name
            }
        );

        if (!outcome.Success)
        {
            var message = outcome.Message ?? "Warp anchor creation failed";
            if (outcome.Exception != null)
                message += ": " + outcome.Exception.Message;
            return BadRequest(message);
        }

        return Ok(
            new WarpAnchorResponse(
                outcome.WarpAnchorConstructId.constructId,
                outcome.WarpAnchorConstructName,
                outcome.WarpAnchorPosition,
                outcome.WarpAnchorPosition.Vec3ToPosition(),
                outcome.Message
            )
        );
    }

    [HttpGet]
    [Route("pending-refresh/{playerId:long}")]
    public IActionResult GetPendingRefresh(ulong playerId)
    {
        var store = ModBase.ServiceProvider.GetRequiredService<Mod.DynamicEncounters.Features.Warp.Interfaces.IWarpDestinationRefreshStore>();
        var script = store.GetAndClearPendingScript(playerId);
        if (string.IsNullOrEmpty(script))
            return NoContent();
        return Content(script, "application/javascript");
    }

    [HttpPost]
    [Route("cooldown")]
    public async Task<IActionResult> SetCooldown([FromBody] SetWarpPropertyRequest request)
    {
        var provider = ModBase.ServiceProvider;
        var warpAnchorService = provider.GetRequiredService<IWarpAnchorService>();
        var outcome = await warpAnchorService.SetWarpCooldown(new SetWarpCooldownCommand
        {
            ConstructId = request.ConstructId,
            ElementTypeName = request.ElementTypeName
        });

        return Ok(outcome);
    }

    public class SetWarpPropertyRequest
    {
        [JsonProperty] public ulong ConstructId { get; set; }
        [JsonProperty] public string ElementTypeName { get; set; } = string.Empty;
    }

    public class WarpAnchorRequestV2
    {
        public ulong PlayerId { get; set; }
        public Vec3 FromPosition { get; set; }
        public Vec3 TargetPosition { get; set; }
        public string ElementTypeName { get; set; } = "WarpDrive";
        /// <summary>If true (default), beacon is public (gameplayTag = "public_warp_beacon"). If false, beacon is private (empty gameplayTag).</summary>
        [JsonProperty] public bool Public { get; set; } = true;
        /// <summary>Minutes after spawn when the beacon construct is despawned. Default 2.</summary>
        [JsonProperty] public double DespawnMinutes { get; set; } = 2;
        /// <summary>Optional custom name for the beacon construct. If null or empty, uses default "[!] &lt;playerName&gt; Warp".</summary>
        [JsonProperty] public string? Name { get; set; }
    }

    public class WarpAnchorResponse(ulong constructId, string constructName, Vec3 position, string positionString, string message)
    {
        public ulong ConstructId { get; set; } = constructId;
        public string ConstructName { get; set; } = constructName;
        public Vec3 Position { get; set; } = position;
        public string PositionString { get; set; } = positionString;
        /// <summary>Feedback message for the player (e.g. beacon at target vs created as far as possible due to 100 SU limit).</summary>
        public string Message { get; set; } = message;
    }
}