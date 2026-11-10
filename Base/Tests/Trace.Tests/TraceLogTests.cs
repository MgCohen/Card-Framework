using Cards.Domain.Shared;
using Cards.Slices.MoveCard;
using Cards.Slices.PlayCard;
using Ecs.Contracts.Commands;
using Ecs.Domain.Shared;
using Ecs.Handlers.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Resources.Domain.Shared;
using Resources.Slices.ChangeResource;
using Trace.Handlers.Commands;
using VSA.Composition;
using VSA.Contracts;
using VSA.Contracts.Commands;

namespace Trace.Tests;

// The Trace hooks record each run of one match: a Send event when the handler starts, a Result event when it
// returns, and a Reject event when a validator stops the message. The parent and the depth come from the Envelope.
// Each host has the plugin (VSA.Sdk gives each module root each plugin root), so a trace records only after Start.
public sealed class TraceLogTests
{
    [Test]
    public async Task OnePlay_GivesEachSendAndResult_InRunOrder()
    {
        using Play play = new Play();
        EntityId card = await play.CardAsync(3);
        EntityId player = await play.PlayerAsync(10);
        play.Trace.Clear();

        await play.Match.Commands.SendAsync(new PlayCardCommand(card, player), CancellationToken.None);

        string[] steps = play.Trace.Events.Select(e => $"{e.Kind} {e.Message.GetType().Name} {e.Depth}").ToArray();
        Assert.That(steps, Is.EqualTo(new[]
        {
            "Send PlayCardCommand 0",
            "Send MoveCardCommand 1",
            "Send ChangeComponent`1 2",
            "Result ChangeComponent`1 2",
            "Result MoveCardCommand 1",
            "Send ChangeResourceCommand 1",
            "Send ChangeComponent`1 2",
            "Result ChangeComponent`1 2",
            "Result ChangeResourceCommand 1",
            "Result PlayCardCommand 0",
        }));
    }

    [Test]
    public async Task OnePlay_EachChildNamesItsParent()
    {
        using Play play = new Play();
        EntityId card = await play.CardAsync(3);
        EntityId player = await play.PlayerAsync(10);
        play.Trace.Clear();

        await play.Match.Commands.SendAsync(new PlayCardCommand(card, player), CancellationToken.None);

        TraceEvent[] sends = play.Trace.Events.Where(e => e.Kind == TraceKind.Send).ToArray();
        Assert.That(sends[0].Parent, Is.Null);
        Assert.That(sends[1].Parent, Is.EqualTo(sends[0].Id));
        Assert.That(sends[2].Parent, Is.EqualTo(sends[1].Id));
        Assert.That(sends[3].Parent, Is.EqualTo(sends[0].Id));
        Assert.That(sends[4].Parent, Is.EqualTo(sends[3].Id));
        Assert.That(sends[1].Message, Is.EqualTo(new MoveCardCommand(card, Zone.Board)));
        Assert.That(sends[3].Message, Is.EqualTo(new ChangeResourceCommand(player, -3, Resource.Mana)));
    }

    [Test]
    public async Task OnePlay_ResultsGiveTheHandlerValue()
    {
        using Play play = new Play();
        EntityId card = await play.CardAsync(3);
        EntityId player = await play.PlayerAsync(10);
        play.Trace.Clear();

        await play.Match.Commands.SendAsync(new PlayCardCommand(card, player), CancellationToken.None);

        string[] outcomes = play.Trace.Events.Where(e => e.Kind == TraceKind.Result).Select(e => e.Outcome).ToArray();
        Assert.That(outcomes, Is.EqualTo(new[] { "Ok(Board)", "Ok", "Ok(7)", "Ok(7)", "Ok" }));
    }

    [Test]
    public async Task ARejectedPlay_GivesOneRejectEvent_WithTheErrorCode()
    {
        using Play play = new Play();
        EntityId card = await play.CardAsync(9);
        EntityId player = await play.PlayerAsync(2);
        play.Trace.Clear();

        PlayCardCommand command = new PlayCardCommand(card, player);
        Result<Unit> result = await play.Match.Commands.SendAsync(command, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(PlayCardErrors.NotEnoughMana));
        Assert.That(play.Trace.Events, Has.Count.EqualTo(1));
        Assert.That(play.Trace.Events[0].Kind, Is.EqualTo(TraceKind.Reject));
        Assert.That(play.Trace.Events[0].Id, Is.EqualTo(result.Envelope!.Id));
        Assert.That(play.Trace.Events[0].Outcome, Is.EqualTo("Error(Cards.NotEnoughMana)"));
    }

    [Test]
    public async Task AHandlerError_IsAResult_WithTheErrorCode()
    {
        using Play play = new Play();
        play.Trace.Clear();

        await play.Match.Commands.SendAsync(new DeleteEntity(new EntityId(99)), CancellationToken.None);

        Assert.That(play.Trace.Events.Select(e => e.Kind), Is.EqualTo(new[] { TraceKind.Send, TraceKind.Result }));
        Assert.That(play.Trace.Events[1].Outcome, Does.StartWith("Error("));
    }

    [Test]
    public async Task Wire_TakesTheIdAndDepthOfTheResultOfTheSameMessage()
    {
        using Play play = new Play();
        EntityId card = await play.CardAsync(3);
        EntityId player = await play.PlayerAsync(10);
        play.Trace.Clear();
        play.Log.Appended += entry => play.Trace.Wire(play.Log.Entries.Count, entry);

        await play.Match.Commands.SendAsync(new PlayCardCommand(card, player), CancellationToken.None);

        TraceEvent[] events = play.Trace.Events.ToArray();
        TraceEvent[] wires = events.Where(e => e.Kind == TraceKind.Wire).ToArray();
        Assert.That(wires, Has.Length.EqualTo(2));
        foreach (TraceEvent wire in wires)
        {
            TraceEvent result = events[Array.IndexOf(events, wire) - 1];
            Assert.That(result.Kind, Is.EqualTo(TraceKind.Result));
            Assert.That(wire.Id, Is.EqualTo(result.Id));
            Assert.That(wire.Depth, Is.EqualTo(result.Depth));
            Assert.That(wire.Message, Is.SameAs(result.Message));
        }

        Assert.That(wires.Select(w => w.Outcome), Is.EqualTo(new[] { "entry 7", "entry 8" }));
    }

    [Test]
    public void Wire_OfAMessageWithNoResult_HasNoId()
    {
        using Play play = new Play();

        play.Trace.Wire(1, new CreateEntity(new EntityId(5)));

        Assert.That(play.Trace.Events[0].Kind, Is.EqualTo(TraceKind.Wire));
        Assert.That(play.Trace.Events[0].Id, Is.EqualTo(0));
        Assert.That(play.Trace.Events[0].Depth, Is.EqualTo(0));
    }

    [Test]
    public void AHandlerThatThrows_GivesSendThenFault()
    {
        using IHost host = VsaBuilder.Create().UseGenericHost().Build();
        using IServiceScope scope = host.Services.CreateScope();
        TraceLog trace = scope.ServiceProvider.GetRequiredService<TraceLog>();
        trace.Start();
        ICommandSender commands = scope.ServiceProvider.GetRequiredService<ICommandSender>();
        ChangeComponent<Mana> write = new ChangeComponent<Mana>(new EntityId(1), new Mana { Value = 1 });

        Func<Task> send = async () => await commands.SendAsync(write, CancellationToken.None);

        Assert.CatchAsync(send);

        Assert.That(trace.Events.Select(e => e.Kind), Is.EqualTo(new[] { TraceKind.Send, TraceKind.Fault }));
        Assert.That(trace.Events[1].Outcome, Is.EqualTo("InvalidOperationException"));
    }

    [Test]
    public void AStepThatThrowsBeforeTheHandler_GivesOnlyFault()
    {
        using IHost host = VsaBuilder.Create().UseGenericHost().Build();
        using IServiceScope scope = host.Services.CreateScope();
        TraceLog trace = scope.ServiceProvider.GetRequiredService<TraceLog>();
        trace.Start();
        ICommandSender commands = scope.ServiceProvider.GetRequiredService<ICommandSender>();
        CreateEntity create = new CreateEntity(default);

        Func<Task> send = async () => await commands.SendAsync(create, CancellationToken.None);

        Assert.CatchAsync(send);

        Assert.That(trace.Events.Select(e => e.Kind), Is.EqualTo(new[] { TraceKind.Fault }));
    }

    [Test]
    public async Task ATraceThatIsNotStarted_RecordsNothing()
    {
        using IHost host = VsaBuilder.Create().UseGenericHost().Build();
        using Match match = new Match(host.Services);
        TraceLog trace = match.Services.GetRequiredService<TraceLog>();

        Result<EntityId> created = await match.Commands.SendAsync(new CreateEntity(default), CancellationToken.None);

        Assert.That(created.IsSuccess, Is.True);
        Assert.That(trace.Recording, Is.False);
        Assert.That(trace.Events, Is.Empty);
    }

    [Test]
    public async Task EachMatch_HasItsOwnTrace()
    {
        using Play first = new Play();
        using Play second = new Play();

        await first.Match.Commands.SendAsync(new CreateEntity(default), CancellationToken.None);

        Assert.That(first.Trace.Events, Is.Not.Empty);
        Assert.That(second.Trace.Events, Is.Empty);
    }
}
