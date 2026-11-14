using Cards.Domain.Shared;
using Cards.Slices.CreateCard;
using Cards.Slices.MoveCard;
using Ecs.Contracts.Commands;
using Ecs.Domain;
using Ecs.Domain.Shared;
using Ecs.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Resources.Domain.Shared;
using Resources.Slices.ChangeResource;
using VSA.Composition;
using VSA.Contracts;
using VSA.Contracts.Commands;
using Wire.Handlers.Commands;

namespace Cards.Tests;

// The done check of P7: a card is made, moved and its Health changes. The gameplay handlers write only with
// primitives, so the WireLog holds one entry for each World change, in order. The test writes Health and the log
// count to the test output before and after each command.
public sealed class CardsAcceptanceTests
{
    [Test]
    public async Task CreateMoveChange_HealthAndLogBeforeAndAfterEachCommand()
    {
        using IHost host = VsaBuilder.Create().UseGenericHost().Build();
        using IServiceScope match = host.Services.GetRequiredService<MatchFactory>().Create();
        IWorldReader world = match.ServiceProvider.GetRequiredService<IWorldReader>();
        WireLog log = match.ServiceProvider.GetRequiredService<WireLog>();

        Result<EntityId> created = await Send(match, new CreateCardCommand(Zone.Hand));
        EntityId card = created.Value;
        Report("CreateCard", world, card, log);
        Assert.That(created.IsSuccess, Is.True);
        Assert.That(world.HasTag<Card>(world.ById(card)), Is.True);
        Assert.That(world.Get<CardZone>(world.ById(card)).Value, Is.EqualTo(Zone.Hand));
        Assert.That(world.Has<Health>(world.ById(card)), Is.False);
        Assert.That(log.Entries, Is.EqualTo(new IMessage[]
        {
            new CreateEntity(card),
            new AddTag<Card>(card),
            new ChangeComponent<CardZone>(card, new CardZone { Value = Zone.Hand }),
        }));

        Result<Unit> moved = await Send(match, new MoveCardCommand(card, Zone.Board));
        Report("MoveCard", world, card, log);
        Assert.That(moved.IsSuccess, Is.True);
        Assert.That(world.Get<CardZone>(world.ById(card)).Value, Is.EqualTo(Zone.Board));
        Assert.That(world.Has<Health>(world.ById(card)), Is.False);
        Assert.That(log.Entries, Has.Count.EqualTo(4));
        CardZone board = new CardZone { Value = Zone.Board };
        Assert.That(log.Entries[3], Is.EqualTo(new ChangeComponent<CardZone>(card, board)));

        Result<int> raised = await Send(match, new ChangeResourceCommand<Health>(card, 5));
        Report("ChangeResource +5", world, card, log);
        Assert.That(raised.Value, Is.EqualTo(5));
        Assert.That(world.Get<Health>(world.ById(card)).Value, Is.EqualTo(5));
        Assert.That(log.Entries, Has.Count.EqualTo(5));
        Assert.That(log.Entries[4], Is.EqualTo(new ChangeComponent<Health>(card, new Health { Value = 5 })));

        Result<int> lowered = await Send(match, new ChangeResourceCommand<Health>(card, -2));
        Report("ChangeResource -2", world, card, log);
        Assert.That(lowered.Value, Is.EqualTo(3));
        Assert.That(world.Get<Health>(world.ById(card)).Value, Is.EqualTo(3));
        Assert.That(log.Entries, Has.Count.EqualTo(6));
        Assert.That(log.Entries[5], Is.EqualTo(new ChangeComponent<Health>(card, new Health { Value = 3 })));

        Result<int> same = await Send(match, new ChangeResourceCommand<Health>(card, 0));
        Report("ChangeResource 0", world, card, log);
        Assert.That(same.Error, Is.EqualTo(PrimitiveErrors.Unchanged));
        Assert.That(world.Get<Health>(world.ById(card)).Value, Is.EqualTo(3));
        Assert.That(log.Entries, Has.Count.EqualTo(6));
    }

    private static Task<Result<T>> Send<T>(IServiceScope match, ICommand<T> command)
    {
        ICommandSender commands = match.ServiceProvider.GetRequiredService<ICommandSender>();
        return commands.SendAsync(command, CancellationToken.None);
    }

    private static void Report(string step, IWorldReader world, EntityId card, WireLog log)
    {
        string health = world.TryGet(world.ById(card), out Health value) ? value.Value.ToString() : "none";
        TestContext.Out.WriteLine($"{step}: Health {health}, log {log.Entries.Count} entries");
    }
}
