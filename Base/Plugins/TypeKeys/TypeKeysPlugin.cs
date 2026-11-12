using Microsoft.Extensions.DependencyInjection;
using TypeKeys.Domain;
using VSA.Module;

namespace TypeKeys;

public sealed class TypeKeysPlugin : Plugin
{
    public override void Configure(ModuleBuilder module)
    {
        foreach (TypeKeyRegistration registration in TypeKeyRegistries.Build(includeTests: false))
        {
            module.Services.AddSingleton(registration.Service, registration.Registry);
        }
    }
}
