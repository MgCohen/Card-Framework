using Ecs.Contracts.Commands;
using Ecs.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VSA.Composition;
using VSA.Contracts;
using VSA.Contracts.Commands;
using Wire.Handlers.Commands;
using Ecs.Handlers.Commands;
using EcsMatch = Ecs.Handlers.Commands.Match;

namespace Wire.Tests.Fixtures;

// One match on the real match owner. The tests also set a state on its World before they send primitives.
internal sealed class Match : IDisposable
{
    private readonly EcsMatch match;

    public Match(IHost host)
    {
        match = new EcsMatch(host.Services);
    }

    public World World => Get<MatchWorld>().World;

    public IServiceProvider Services => match.Services;

    public WireLog Log => Get<WireLog>();

    public GroupService Groups => Get<GroupService>();

    public WireCatalog Catalog => Get<WireCatalog>();

    public ICommandSender Bus => match.Commands;

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
        match.Dispose();
    }

    private T Get<T>()
        where T : notnull
    {
        return match.Services.GetRequiredService<T>();
    }
}
