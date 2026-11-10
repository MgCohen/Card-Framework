using Microsoft.Extensions.DependencyInjection;
using Resources.Domain.Shared;
using VSA.Module;
using Wire.Handlers.Commands;

namespace Resources;

public sealed class ResourcesModule : Module
{
    public override void Configure(ModuleBuilder module)
    {
        module.Services.AddSingleton(WireTypes.Of(t => t.Component<Health>().Component<Mana>()));
    }
}
