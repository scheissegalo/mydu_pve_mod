using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Mod.DynamicEncounters.Overrides.Common;
using Mod.DynamicEncounters.Overrides.Common.Interfaces;
using Mod.DynamicEncounters.Overrides.Common.Services;
using NQ;

namespace Mod.DynamicEncounters.Overrides.Actions.Party;

public class RenderPartyAppAction : IModActionHandler
{
    public async Task HandleAction(ulong playerId, ModAction action)
    {
        var injection = ModServiceProvider.Get<IMyDuInjectionService>();
        var logger = ModServiceProvider.GetExternal<ILoggerFactory>().CreateLogger<RenderPartyAppAction>();

        try
        {
            logger.LogInformation("RenderPartyAppAction: Injecting CreatePartyRootDivJs");
            await injection.InjectJs(playerId, Resources.CreatePartyRootDivJs);
            await Task.Delay(100);
            
            logger.LogInformation("RenderPartyAppAction: Injecting PartyAppCss");
            await injection.InjectCss(playerId, Resources.PartyAppCss);
            await Task.Delay(100);
            
            logger.LogInformation("RenderPartyAppAction: Injecting PartyAppJs");
            await injection.InjectJs(playerId, Resources.PartyAppJs);
            logger.LogInformation("RenderPartyAppAction: Completed successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RenderPartyAppAction: Failed - {ExceptionType}: {Message}", ex.GetType().Name, ex.Message);
            throw;
        }
    }
}