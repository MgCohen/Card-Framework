using Microsoft.Extensions.DependencyInjection;
using TypeKeys.Domain;
using VSA.Module;

namespace TypeKeys;

public sealed class TypeKeysModule : Module
{
    public override void Configure(ModuleBuilder module)
    {
        foreach (ModuleRoot root in ModuleDiscovery.FindRoots())
        {
            foreach (string layer in root.Layers)
            {
                System.Reflection.Assembly.Load(new System.Reflection.AssemblyName(layer));
            }
        }

        foreach (TypeKeyRegistration registration in TypeKeyRegistries.Build(false))
        {
            module.Services.AddSingleton(registration.Service, registration.Registry);
        }
    }
}
