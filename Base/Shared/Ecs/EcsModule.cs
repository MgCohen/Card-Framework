using Ecs.Domain;
using VSA.Module;

namespace Ecs;

public sealed class EcsModule : Module
{
    public override void Configure(ModuleBuilder module)
    {
        module.Services.AddSingleton<IWorldWriter>(_ => new World());
        module.Services.AddSingleton<IWorldReader>(resolver => ((World)resolver.Require<IWorldWriter>()).Reader);
    }
}
