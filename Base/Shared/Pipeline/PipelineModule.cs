using Pipeline.Slices.Run;
using VSA.Contracts.Commands;
using VSA.Module;

namespace Pipeline;

public sealed class PipelineModule : Module
{
    public override void Configure(ModuleBuilder module)
    {
        module.Services.AddSingleton<PipelineRuns>();
        module.Services.AddScoped(resolver => new PipelineServices(resolver.Resolve));
        foreach (Type validator in PipelineValidators.Find(includeTests: false))
        {
            module.Services.Add(typeof(Validator), validator, VsaLifetime.Singleton);
        }

        module.Services.AddSingleton(resolver => new PipelineValidators(resolver.ResolveAll<Validator>()));
        module.Replace<ICommandSender, PipelineCommandSender>();
    }
}
