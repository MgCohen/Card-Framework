using Ecs.Contracts.Commands;
using Ecs.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VSA.Composition;
using VSA.Contracts;
using VSA.Contracts.Commands;
using Wire.Handlers.Commands;

namespace Wire.Tests.Fixtures;

// One match: a scope of the host that gives one World, its reader and writer, and one log.
// P7 owns the real match scope; this fixture registers the same three services for the tests.
internal sealed class Match : IDisposable
{
    private readonly IServiceScope scope;

    public Match(IHost host)
    {
        scope = host.Services.CreateScope();
    }

    public World World => Get<World>();

    public WireLog Log => Get<WireLog>();

    public GroupService Groups => Get<GroupService>();

    public WireCatalog Catalog => Get<WireCatalog>();

    public ICommandSender Bus => Get<ICommandSender>();

    public static IHost Host()
    {
        return VsaBuilder.Create().UseGenericHost().Services(Register).Build();
    }

    public static WireTypes Keys()
    {
        return WireTypes.Of(t => t.Component<Health>().Component<Armor>().Tag<Marked>());
    }

    public Task<Result<T>> Send<T>(IPrimitive<T> primitive)
    {
        return Bus.SendAsync(primitive, CancellationToken.None);
    }

    public void Dispose()
    {
        scope.Dispose();
    }

    private static void Register(IServiceCollection services)
    {
        services.AddSingleton(Keys());
        services.AddScoped(provider => provider.GetRequiredService<IWorldFactory>().Create());
        services.AddScoped<IWorldWriter>(provider => provider.GetRequiredService<World>());
        services.AddScoped(provider => provider.GetRequiredService<World>().Reader);
    }

    private T Get<T>()
        where T : notnull
    {
        return scope.ServiceProvider.GetRequiredService<T>();
    }
}
