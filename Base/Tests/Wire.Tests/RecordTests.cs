using Ecs.Contracts.Commands;
using Ecs.Domain.Shared;
using Microsoft.Extensions.Hosting;
using VSA.Contracts;
using Wire.Tests.Fixtures;

namespace Wire.Tests;

// RecordStep appends each accepted primitive to the log of its match scope, and nothing else.
public sealed class RecordTests
{
    private IHost host = null!;
    private Match match = null!;

    [SetUp]
    public void Start()
    {
        host = Match.Host();
        match = new Match(host);
    }

    [TearDown]
    public void Stop()
    {
        match?.Dispose();
        host?.Dispose();
    }

    [Test]
    public async Task Record_Accepted_AppendsTheMessage()
    {
        EntityId id = (await match.Send(new CreateEntity(default))).Value;
        ChangeComponent<Health> change = new ChangeComponent<Health>(id, new Health { Value = 27 });

        await match.Send(change);

        Assert.That(match.Log.Entries[^1], Is.SameAs(change));
    }

    [Test]
    public async Task Record_HandlerError_AppendsNothing()
    {
        EntityId id = (await match.Send(new CreateEntity(default))).Value;
        await match.Send(new ChangeComponent<Health>(id, new Health { Value = 27 }));
        int count = match.Log.Entries.Count;

        await match.Send(new ChangeComponent<Health>(id, new Health { Value = 27 }));
        await match.Send(new DeleteEntity(new EntityId(99)));

        Assert.That(match.Log.Entries.Count, Is.EqualTo(count));
    }

    [Test]
    public async Task Record_Accepted_RaisesAppendedOnce()
    {
        List<IMessage> appended = new List<IMessage>();
        match.Log.Appended += appended.Add;

        await match.Send(new CreateEntity(default));

        Assert.That(appended, Has.Count.EqualTo(1));
        Assert.That(appended[0], Is.SameAs(match.Log.Entries[0]));
    }

    [Test]
    public async Task Record_EachPrimitive_AppendsItsOwnType()
    {
        EntityId id = (await match.Send(new CreateEntity(default))).Value;
        await match.Send(new ChangeComponent<Health>(id, new Health { Value = 1 }));
        await match.Send(new AddTag<Marked>(id));
        await match.Send(new RemoveTag<Marked>(id));
        await match.Send(new RemoveComponent<Health>(id));
        await match.Send(new DeleteEntity(id));

        Type[] types = match.Log.Entries.Select(e => e.GetType()).ToArray();

        Assert.That(types, Is.EqualTo(new[]
        {
            typeof(CreateEntity),
            typeof(ChangeComponent<Health>),
            typeof(AddTag<Marked>),
            typeof(RemoveTag<Marked>),
            typeof(RemoveComponent<Health>),
            typeof(DeleteEntity),
        }));
    }

    [Test]
    public async Task Record_CreateEntityDefaultId_EntryHasRealId()
    {
        EntityId expected = match.World.NextId();

        await match.Send(new CreateEntity(default));

        Assert.That(((CreateEntity)match.Log.Entries[0]).Entity, Is.EqualTo(expected));
    }

    [Test]
    public async Task Record_TwoMatches_SeparateLogs()
    {
        using Match other = new Match(host);

        await match.Send(new CreateEntity(default));

        Assert.That(match.Log.Entries, Has.Count.EqualTo(1));
        Assert.That(other.Log.Entries, Is.Empty);
        Assert.That(other.World.Count, Is.Zero);
    }
}
