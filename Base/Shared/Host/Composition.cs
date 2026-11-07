using Host.Domain.Shared;
using Host.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Host;

public static class Composition
{
    public static ServiceProvider Build(PeerRole role, ITransport? transport, params IHostModule[] modules)
    {
        HashSet<Type> types = new HashSet<Type>();
        foreach (IHostModule module in modules)
        {
            if (module is null)
            {
                throw new ArgumentException("A module is null.", nameof(modules));
            }

            if (!types.Add(module.GetType()))
            {
                throw new ArgumentException($"Module {module.GetType().FullName} is given two times.", nameof(modules));
            }
        }

        ServiceCollection services = new ServiceCollection();
        services.AddSingleton(typeof(PeerRole), role);
        if (transport is not null)
        {
            services.AddSingleton(transport);
        }

        foreach (IHostModule module in modules)
        {
            module.Configure(services, role);
        }

        ServiceProviderOptions options = new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true };
        return services.BuildServiceProvider(options);
    }
}
