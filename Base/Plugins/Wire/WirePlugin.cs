using Ecs.Handlers.Commands;
using Microsoft.Extensions.DependencyInjection;
using VSA.Handlers;
using VSA.Module;
using Wire.Handlers.Commands;

namespace Wire;

public sealed class WirePlugin : Plugin
{
    public override void Configure(ModuleBuilder module)
    {
        module.Services.AddSingleton(provider => WireTypes.OfHandlers(provider.GetRequiredService<ModuleTypes>()
            .Implementations(typeof(PrimitiveHandler<,>))
            .Select(handler => handler.Implementation)));
        module.Services.AddSingleton(provider => new WireCatalog(provider.GetServices<WireTypes>()));
        module.Services.AddScoped<WireLog>();
        module.Services.AddScoped<GroupService>();
        module.Services.AddPipeline(new PrimitivePipelineBuilder());
    }
}
