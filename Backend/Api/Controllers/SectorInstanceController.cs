using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Mod.DynamicEncounters.Features.Common.Interfaces;
using Mod.DynamicEncounters.Features.Scripts.Actions.Interfaces;
using Mod.DynamicEncounters.Features.Sector.Data;
using Mod.DynamicEncounters.Features.Sector.Interfaces;
using Mod.DynamicEncounters.Features.Sector.Services;
using NQ;
using Swashbuckle.AspNetCore.Annotations;

namespace Mod.DynamicEncounters.Api.Controllers;

[Route("sector/instance")]
public class SectorInstanceController(IServiceProvider provider) : Controller
{
    private readonly ISectorInstanceRepository _repository = provider.GetRequiredService<ISectorInstanceRepository>();
    
    [HttpGet]
    [Route("")]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _repository.GetAllAsync());
    }

    [HttpGet]
    [Route("active")]
    public async Task<IActionResult> GetActiveSector()
    {
        return Ok(await _repository.FindActiveAsync());
    }

    [HttpGet]
    [Route("{id:guid}/occupants")]
    [SwaggerOperation("Players, player constructs near the instance, and NPC construct handles for that sector (debug / observability)")]
    public async Task<IActionResult> GetOccupants(Guid id)
    {
        var sectorInstance = await _repository.FindById(id);
        if (sectorInstance == null)
        {
            return NotFound();
        }

        var spatial = provider.GetRequiredService<IConstructSpatialHashRepository>();
        var handleRepo = provider.GetRequiredService<IConstructHandleRepository>();

        var playerConstructIds = (await spatial.FindAllPlayerLiveConstructsNearPosition(
            sectorInstance.Sector,
            SectorPoolManager.EncounterZonePlayerProximityMeters
        )).Distinct().ToList();

        var playerIds = await spatial.GetDistinctPlayerIdsForConstructs(playerConstructIds);
        var organizationOnlyConstructCount =
            await spatial.GetOrganizationOnlyConstructCountAmong(playerConstructIds);

        var npcHandles = (await handleRepo.FindInSectorAsync(sectorInstance.Sector)).Select(h => new
        {
            constructId = h.ConstructId,
            constructDefinitionId = h.ConstructDefinitionId,
            constructName = h.JsonProperties?.ConstructName,
            tags = h.JsonProperties?.Tags
        }).ToList();

        return Ok(new
        {
            sectorInstanceId = sectorInstance.Id,
            sector = new { x = sectorInstance.Sector.x, y = sectorInstance.Sector.y, z = sectorInstance.Sector.z },
            name = sectorInstance.Name,
            onSectorEnterScript = sectorInstance.OnSectorEnterScript,
            startedAt = sectorInstance.StartedAt,
            active = sectorInstance.StartedAt.HasValue,
            playerIds,
            playerConstructIds,
            organizationOnlyConstructCount,
            npcConstructHandles = npcHandles
        });
    }

    [HttpPost]
    [Route("activate")]
    public async Task<IActionResult> ActivateSector([FromBody] SectorRequest request)
    {
        SectorInstance sectorInstance;

        if (request.Sector.HasValue)
        {
            sectorInstance = await _repository.FindBySector(request.Sector.Value);
        }
        else if (request.Id.HasValue)
        {
            sectorInstance = await _repository.FindById(request.Id.Value);
        }
        else
        {
            return BadRequest();
        }

        if (sectorInstance == null)
        {
            return NotFound();
        }
        
        var sectorPoolManager = provider.GetRequiredService<ISectorPoolManager>();

        await sectorPoolManager.ForceActivateSector(sectorInstance.Id);

        return Ok(sectorInstance);
    }
    
    [HttpPost]
    [Route("expire")]
    public async Task<IActionResult> ExpireSector([FromBody] SectorRequest request)
    {
        SectorInstance sectorInstance;

        if (request.Sector.HasValue)
        {
            sectorInstance = await _repository.FindBySector(request.Sector.Value);
        }
        else if (request.Id.HasValue)
        {
            sectorInstance = await _repository.FindById(request.Id.Value);
        }
        else
        {
            return BadRequest();
        }
        
        if (sectorInstance == null)
        {
            return NotFound();
        }
        
        var sectorPoolManager = provider.GetRequiredService<ISectorPoolManager>();

        await sectorPoolManager.SetExpirationFromNow(sectorInstance.Sector, TimeSpan.Zero);

        return Ok();
    }

    [HttpPost]
    [Route("expire/all")]
    public async Task<IActionResult> ExpireAll()
    {
        await _repository.ExpireAllAsync();

        return Ok();
    }
    
    [HttpPost]
    [Route("expire/force/all")]
    public async Task<IActionResult> ForceExpireAll()
    {
        await _repository.ForceExpireAllAsync();

        return Ok();
    }
    
    [HttpPost]
    [Route("load")]
    [SwaggerOperation("Manually trigger loading of unloaded sectors (runs onLoadScript)")]
    public async Task<IActionResult> LoadSectors()
    {
        var sectorPoolManager = provider.GetRequiredService<ISectorPoolManager>();
        await sectorPoolManager.LoadUnloadedSectors();
        return Ok("Sector loading triggered");
    }
    
    public class SectorRequest
    {
        public Vec3? Sector { get; set; }
        public Guid? Id { get; set; }
    }
}