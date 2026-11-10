using Microsoft.Extensions.DependencyInjection;
using Trace.Handlers.Commands;
using VSA.Module;

namespace Trace;

public sealed class TracePlugin : Plugin
{
    public override void Configure(ModuleBuilder module)
    {
        module.Services.AddScoped<TraceLog>();
    }
}
