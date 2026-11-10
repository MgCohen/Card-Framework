using Cards.Domain.Shared;
using Cards.Slices.CreateCard;
using Ecs.Contracts.Commands;
using Ecs.Domain.Shared;
using Ecs.Handlers.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Resources.Domain.Shared;
using Resources.Slices.ChangeResource;
using Trace.Handlers.Commands;
using VSA.Composition;
using Wire.Handlers.Commands;

namespace Trace.Tests;

// One host and one match with a started trace, as App.Cli makes them.
public sealed class Play : IDisposable
{
    private readonly IHost host;

    public Play()
    {
        host = VsaBuilder.Create().UseGenericHost().Build();
        Match = new Match(host.Services);
        Trace = Match.Services.GetRequiredService<TraceLog>();
        Trace.Start();
        Log = Match.Services.GetRequiredService<WireLog>();
    }

    public Match Match { get; }

    public TraceLog Trace { get; }

    public WireLog Log { get; }

    public async Task<EntityId> CardAsync(int cost)
    {
        return (await Match.Commands.SendAsync(new CreateCardCommand(Zone.Hand, cost), CancellationToken.None)).Value;
    }

    public async Task<EntityId> PlayerAsync(int mana)
    {
        EntityId player = (await Match.Commands.SendAsync(new CreateEntity(default), CancellationToken.None)).Value;
        await Match.Commands.SendAsync(new ChangeResourceCommand(player, mana, Resource.Mana), CancellationToken.None);
        return player;
    }

    public void Dispose()
    {
        Match.Dispose();
        host.Dispose();
    }
}
