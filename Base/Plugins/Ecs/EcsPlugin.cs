using Ecs.Domain;
using Microsoft.Extensions.DependencyInjection;
using VSA.Module;

namespace Ecs;

public sealed class EcsPlugin : Plugin
{
    public override void Configure(ModuleBuilder module)
    {
        module.Services.AddSingleton<IWorldFactory, WorldFactory>();
    }
}
