using Ecs.Domain;
using Microsoft.Extensions.DependencyInjection;
using VSA.Module;

namespace Ecs;

public sealed class EcsModule : Module
{
    public override void Configure(ModuleBuilder module)
    {
        module.Services.AddSingleton<IWorldWriter>(_ => new World());
        module.Services.AddSingleton<IWorldReader>(
            provider => ((World)provider.GetRequiredService<IWorldWriter>()).Reader);
    }
}
