using Ecs.Domain;
using Ecs.Handlers.Commands;
using Microsoft.Extensions.DependencyInjection;
using VSA.Module;

namespace Ecs;

public sealed class EcsPlugin : Plugin
{
    public override void Configure(ModuleBuilder module)
    {
        module.Services.AddSingleton<IWorldFactory, WorldFactory>();
        module.Services.AddSingleton<MatchFactory>();
        module.Services.AddScoped<MatchWorld>();
        module.Services.AddScoped(provider => provider.GetRequiredService<MatchWorld>().Reader);
    }
}
