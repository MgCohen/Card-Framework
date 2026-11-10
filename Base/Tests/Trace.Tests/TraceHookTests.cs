using Cards.Domain.Shared;
using Cards.Slices.PlayCard;
using Ecs.Contracts.Commands;
using Ecs.Domain.Shared;
using Microsoft.Extensions.DependencyInjection;
using Resources.Domain.Shared;
using Trace.Handlers.Commands;
using VSA.Contracts;
using VSA.Handlers;

namespace Trace.Tests;

// B6 lets the Trace plugin hook each primitive, because each Trace hook only reads: it gives back the same
// message instance and never rejects. This test runs each hook type of the Trace assemblies on probe messages.
public sealed class TraceHookTests
{
    private static readonly IMessage[] Probes =
    {
        new CreateEntity(new EntityId(1)),
        new DeleteEntity(new EntityId(1)),
        new ChangeComponent<Mana>(new EntityId(1), new Mana { Value = 2 }),
        new AddTag<Card>(new EntityId(1)),
        new PlayCardCommand(new EntityId(1), new EntityId(2)),
    };

    [Test]
    public void TheTraceAssembliesHaveOneBeforeAndOneAfter()
    {
        Assert.That(Hooks(typeof(Before<>)), Is.EqualTo(new[] { typeof(TraceBefore) }));
        Assert.That(Hooks(typeof(After<>)), Is.EqualTo(new[] { typeof(TraceAfter) }));
    }

    [Test]
    public async Task EachBefore_GivesBackTheSameMessage_AndDoesNotReject()
    {
        using Play play = new Play();
        foreach (Type type in Hooks(typeof(Before<>)))
        {
            Before<IMessage> hook = (Before<IMessage>)ActivatorUtilities.CreateInstance(play.Match.Services, type);
            foreach (IMessage probe in Probes)
            {
                Envelope<IMessage> envelope = Envelope.For(probe, play.Match.Services);

                IMessage back = await hook.BeforeAsync(envelope);

                Assert.That(back, Is.SameAs(probe), $"{type.Name} on {probe.GetType().Name}");
                Assert.That(envelope.State, Is.Not.EqualTo(EnvelopeState.Rejected));
                Assert.That(envelope.Message, Is.SameAs(probe));
            }
        }
    }

    [Test]
    public async Task EachAfter_KeepsTheMessage_AndDoesNotReject()
    {
        using Play play = new Play();
        foreach (Type type in Hooks(typeof(After<>)))
        {
            After<IMessage> hook = (After<IMessage>)ActivatorUtilities.CreateInstance(play.Match.Services, type);
            foreach (IMessage probe in Probes)
            {
                Envelope<IMessage> envelope = Envelope.For(probe, play.Match.Services);

                await hook.AfterAsync(envelope);

                Assert.That(envelope.State, Is.Not.EqualTo(EnvelopeState.Rejected), type.Name);
                Assert.That(envelope.Error, Is.Null);
                Assert.That(envelope.Message, Is.SameAs(probe));
            }
        }
    }

    private static Type[] Hooks(Type hookBase)
    {
        return typeof(TraceLog).Assembly.GetTypes()
            .Concat(typeof(TracePlugin).Assembly.GetTypes())
            .Where(t => Bases(t).Any(b => b.IsGenericType && b.GetGenericTypeDefinition() == hookBase))
            .ToArray();
    }

    private static IEnumerable<Type> Bases(Type type)
    {
        for (Type? b = type.BaseType; b != null; b = b.BaseType)
        {
            yield return b;
        }
    }
}
