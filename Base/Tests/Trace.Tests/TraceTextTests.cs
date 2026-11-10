using System.Globalization;
using Cards.Domain.Shared;
using Cards.Slices.PlayCard;
using Ecs.Contracts.Commands;
using Ecs.Domain.Shared;
using Resources.Domain.Shared;
using Resources.Slices.ChangeResource;
using Trace.Handlers.Commands;

namespace Trace.Tests;

// The text form of the trace: a fixed tag on each line, an indent for each parent, and a short payload.
public sealed class TraceTextTests
{
    [Test]
    public void Name_WritesTheTypeArguments()
    {
        Assert.That(TraceText.Name(typeof(ChangeComponent<CardZone>)), Is.EqualTo("ChangeComponent<CardZone>"));
        Assert.That(TraceText.Name(typeof(PlayCardCommand)), Is.EqualTo("PlayCardCommand"));
    }

    [Test]
    public void Payload_WritesEachProperty_AndTheValueOfAnIdOrAValueStruct()
    {
        PlayCardCommand play = new PlayCardCommand(new EntityId(1), new EntityId(3));
        CardZone board = new CardZone { Value = Zone.Board };
        ChangeComponent<CardZone> move = new ChangeComponent<CardZone>(new EntityId(1), board);
        ChangeResourceCommand pay = new ChangeResourceCommand(new EntityId(3), -3, Resource.Mana);

        Assert.That(TraceText.Payload(play), Is.EqualTo("{ Card: 1, Player: 3 }"));
        Assert.That(TraceText.Payload(move), Is.EqualTo("{ Entity: 1, Value: Board }"));
        Assert.That(TraceText.Payload(pay), Is.EqualTo("{ Target: 3, Delta: -3, Resource: Mana }"));
    }

    [Test]
    public void Value_OfAValueThatCouldRecurseOrThrow_GivesShortText()
    {
        DateTime time = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);

        Assert.That(TraceText.Value(new[] { 1, 2 }), Is.EqualTo("[Int32[]]"));
        Assert.That(TraceText.Value(time), Is.EqualTo(time.ToString(CultureInfo.InvariantCulture)));
        Assert.That(TraceText.Value(new Throws()), Is.EqualTo("{ Bad: <InvalidOperationException> }"));
        Assert.That(TraceText.Value(new Loop()), Does.Contain("{ ... }"));
        Assert.That(TraceText.Value(typeof(string)), Is.Not.Empty);
    }

    [Test]
    public void Legend_NamesEachTag()
    {
        string legend = string.Join("\n", TraceText.Legend);

        Assert.That(TraceText.Legend[0], Is.EqualTo("LEGEND"));
        foreach (string tag in new[] { "[SEND]", "[REJECT]", "[RESULT]", "[FAULT]", "[WIRE]", "[WORLD]" })
        {
            Assert.That(legend, Does.Contain(tag));
        }
    }

    [Test]
    public void Lines_StartWithTheTag_IndentedByDepth()
    {
        PlayCardCommand play = new PlayCardCommand(new EntityId(1), new EntityId(3));
        CreateEntity create = new CreateEntity(new EntityId(4));
        TraceEvent[] events =
        {
            new TraceEvent(TraceKind.Send, 14, null, 0, play, string.Empty),
            new TraceEvent(TraceKind.Send, 15, 14, 1, create, string.Empty),
            new TraceEvent(TraceKind.Result, 15, 14, 1, create, "Ok(4)"),
            new TraceEvent(TraceKind.Wire, 15, 14, 1, create, "entry 3"),
            new TraceEvent(TraceKind.Result, 14, null, 0, play, "Ok"),
            new TraceEvent(TraceKind.Reject, 16, null, 0, play, "Error(Cards.NotInHand)"),
            new TraceEvent(TraceKind.Fault, 17, null, 0, play, "InvalidOperationException"),
        };

        string[] lines = TraceText.Lines(events).ToArray();

        Assert.That(lines[0], Does.StartWith("[SEND]").And.Contain("PlayCardCommand").And.Contain("msg=14"));
        Assert.That(lines[0], Does.Contain("payload={ Card: 1, Player: 3 }").And.Not.Contain("parent="));
        Assert.That(lines[1], Does.StartWith("  [SEND]").And.Contain("msg=15").And.Contain("parent=14"));
        Assert.That(lines[2], Does.StartWith("  [RESULT]").And.Contain("CreateEntity").And.EndWith("Ok(4)"));
        Assert.That(lines[3], Does.StartWith("  [WIRE]").And.Contain("entry 3").And.Contain("msg=15"));
        Assert.That(lines[4], Does.StartWith("[RESULT]").And.EndWith("Ok"));
        Assert.That(lines[5], Does.StartWith("[REJECT]").And.Contain("payload=").And.EndWith("Error(Cards.NotInHand)"));
        Assert.That(lines[6], Does.StartWith("[FAULT]").And.EndWith("InvalidOperationException"));
    }

    private sealed class Throws
    {
        public int Bad => throw new InvalidOperationException();
    }

    private sealed class Loop
    {
        public Loop Next => this;

        public int Size => 1;
    }
}
