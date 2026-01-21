using System;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Mod.DynamicEncounters.Api.Controllers.Validators;
using Mod.DynamicEncounters.Common;
using Mod.DynamicEncounters.Database.Interfaces;
using Mod.DynamicEncounters.Features.Sector.Data;
using Mod.DynamicEncounters.Features.Sector.Interfaces;
using Swashbuckle.AspNetCore.Annotations;
using NameSanitationHelper = Mod.DynamicEncounters.Common.Helpers.NameSanitationHelper;

namespace Mod.DynamicEncounters.Api.Controllers;

[Route("sector/encounter")]
public class SectorEncountersController(IServiceProvider provider) : Controller
{
    private readonly ISectorEncounterRepository _repository =
        provider.GetRequiredService<ISectorEncounterRepository>();
    
    [SwaggerOperation("Retrieves all sector encounters")]
    [HttpGet]
    [Route("")]
    public async Task<IActionResult> GetAll()
    {
        return Json(await _repository.GetAllAsync());
    }
    
    [SwaggerOperation("Delete a sector encounter by ID")]
    [HttpDelete]
    [Route("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var connectionFactory = provider.GetRequiredService<IPostgresConnectionFactory>();
        using var db = connectionFactory.Create();
        db.Open();
        
        await db.ExecuteAsync(
            "DELETE FROM public.mod_sector_encounter WHERE id = @id",
            new { id }
        );
        
        return Ok();
    }
    
    [SwaggerOperation("Set encounter active status")]
    [HttpPatch]
    [Route("{id}/active")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetActiveRequest request)
    {
        var connectionFactory = provider.GetRequiredService<IPostgresConnectionFactory>();
        using var db = connectionFactory.Create();
        db.Open();
        
        await db.ExecuteAsync(
            "UPDATE public.mod_sector_encounter SET active = @active WHERE id = @id",
            new { id, active = request.Active }
        );
        
        return Ok();
    }
    
    public class SetActiveRequest
    {
        public bool Active { get; set; }
    }

    [SwaggerOperation("Add a new wreck sector encounter to the system")]
    [HttpPut]
    [Route("wreck")]
    public async Task<IActionResult> Add(AddWreckSectorEncounterRequest request)
    {
        var validator = new AddWreckSectorEncounterRequestValidator();
        var validationResult = await validator.ValidateAsync(request);

        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult);
        }
        
        request.Sanitize();
        
        await _repository.AddAsync(
            new SectorEncounterItem
            {
                Id = Guid.NewGuid(),
                Name = request.WreckScript,
                OnLoadScript = request.WreckScript,
                OnSectorEnterScript = "expire-sector-default",
                Active = true
            }
        );
        
        return Created();
    }
    
    [SwaggerOperation("Add a new NPC sector encounter to the system")]
    [HttpPut]
    [Route("npc")]
    public async Task<IActionResult> Add(AddNpcSectorEncounterRequest request)
    {
        var validator = new AddNpcSectorEncounterRequestValidator();
        var validationResult = await validator.ValidateAsync(request);

        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult);
        }
        
        request.Sanitize();
        
        await _repository.AddAsync(
            new SectorEncounterItem
            {
                Id = Guid.NewGuid(),
                Name = $"{request.POIScript} / {request.NpcScript}",
                OnLoadScript = request.POIScript,
                OnSectorEnterScript = request.NpcScript,
                Active = true
            }
        );
        
        return Created();
    }

    [SwaggerSchema(Required = [nameof(WreckScript)])]
    public class AddWreckSectorEncounterRequest
    {
        [SwaggerSchema(Description = "Wreck script to run")]
        public string WreckScript { get; set; }

        public void Sanitize()
        {
            WreckScript = NameSanitationHelper.SanitizeName(WreckScript);
        }
    }
    
    [SwaggerSchema(Required = [nameof(NpcScript), nameof(POIScript)])]
    public class AddNpcSectorEncounterRequest
    {
        [SwaggerSchema(Description = "NPC script to run")]
        public string NpcScript { get; set; }
        
        [SwaggerSchema(Description = "POI script to run. It should be a dynamic wreck so it can appear on the Points of Interest of the game")]
        public string POIScript { get; set; }

        public void Sanitize()
        {
            NpcScript = NameSanitationHelper.SanitizeName(NpcScript);
            POIScript = NameSanitationHelper.SanitizeName(POIScript);
        }
    }
}