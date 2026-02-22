using Microsoft.Extensions.DependencyInjection;
using Mod.DynamicEncounters.Features.AlienWar.Interfaces;
using Mod.DynamicEncounters.Features.AlienWar.Repository;
using Mod.DynamicEncounters.Features.AlienWar.Services;

namespace Mod.DynamicEncounters.Features.AlienWar;

public static class AlienWarRegistration
{
    public static void RegisterAlienWar(this IServiceCollection services)
    {
        services.AddSingleton<IAlienCoreShieldService, AlienCoreShieldService>();
        services.AddSingleton<IAlienWarStateService, AlienWarStateService>();
        services.AddSingleton<IAlienWarEventRepository, AlienWarEventRepository>();
    }
}
