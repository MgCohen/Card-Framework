using Ecs.Contracts.Commands;
using Ecs.Domain;
using Ecs.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VSA.Composition;
using VSA.Contracts;
using VSA.Contracts.Commands;
using Wire.Handlers.Commands;

namespace Wire.Tests.Fixtures;

// One match from the real MatchFactory. The tests also set a state on its World before they send primitives.
internal sealed class Match : IDisposable
{
    private readonly IServiceScope scope;

    public Match(IHost host)
    {
        scope = host.Services.GetRequiredService<MatchFactory>().Create();
    }

    public World World => Get<MatchWorld>().World;

    public IServiceProvider Services => scope.ServiceProvider;

    public WireLog Log => Get<WireLog>();

    public GroupService Groups => Get<GroupService>();

    public WireCatalog Catalog => Get<WireCatalog>();

    public ICommandSender Bus => Get<ICommandSender>();

    public static IHost Host()
    {
        return VsaBuilder.Create().UseGenericHost().Services(services => services.AddSingleton(Keys())).Build();
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

    private T Get<T>()
        where T : notnull
    {
        return scope.ServiceProvider.GetRequiredService<T>();
    }
}
