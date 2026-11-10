using Ecs.Contracts.Commands;
using Ecs.Domain.Shared;
using Microsoft.Extensions.Hosting;
using VSA.Contracts;
using Wire.Contracts.Commands;
using Wire.Tests.Fixtures;

namespace Wire.Tests;

// GroupService is the copy of Assets/Blocks: brackets in the log, with the block id as the 1-based position.
public sealed class GroupTests
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
    public async Task Group_OpenClose_BracketsEntries()
    {
        BlockId id = match.Groups.Open(new EntityId(9));
        await match.Send(new CreateEntity(default));
        match.Groups.Close(id);

        IReadOnlyList<IMessage> entries = match.Log.Entries;

        Assert.That(id, Is.EqualTo(new BlockId(1)));
        Assert.That(entries[0], Is.EqualTo(new GroupStart(new EntityId(9), default)));
        Assert.That(entries[1], Is.TypeOf<CreateEntity>());
        Assert.That(entries[2], Is.EqualTo(new GroupEnd(id, GroupOutcome.Completed)));
        Assert.That(match.Groups.Current, Is.EqualTo(default(BlockId)));
    }

    [Test]
    public void Group_Nested_CurrentIsInner()
    {
        BlockId outer = match.Groups.Open();
        BlockId inner = match.Groups.Open(trigger: outer);

        Assert.That(match.Groups.Current, Is.EqualTo(inner));
        Assert.That(match.Log.Entries[1], Is.EqualTo(new GroupStart(default, outer)));

        match.Groups.Close(inner);

        Assert.That(match.Groups.Current, Is.EqualTo(outer));
    }

    [Test]
    public void Group_CloseWithOpenChild_ErrorsAndThrows()
    {
        BlockId outer = match.Groups.Open();
        BlockId inner = match.Groups.Open();

        Assert.Throws<InvalidOperationException>(new Action(() => match.Groups.Close(outer)));

        Assert.That(match.Log.Entries[^2], Is.EqualTo(new GroupEnd(inner, GroupOutcome.Errored)));
        Assert.That(match.Log.Entries[^1], Is.EqualTo(new GroupEnd(outer, GroupOutcome.Errored)));
        Assert.That(match.Groups.Current, Is.EqualTo(default(BlockId)));
    }

    [Test]
    public void Group_CloseNotOpen_Throws()
    {
        Assert.Throws<InvalidOperationException>(new Action(() => match.Groups.Close(new BlockId(1))));
    }

    [Test]
    public void Group_Create_ReturnsResultAndCloses()
    {
        int result = match.Groups.Create(() => 5);

        Assert.That(result, Is.EqualTo(5));
        Assert.That(match.Log.Entries[^1], Is.EqualTo(new GroupEnd(new BlockId(1), GroupOutcome.Completed)));
    }

    [Test]
    public void Group_CreateThrows_ClosedErrored()
    {
        Assert.Throws<InvalidOperationException>(new Action(() => match.Groups.Create(() => throw new InvalidOperationException("fail"))));

        Assert.That(match.Log.Entries[^1], Is.EqualTo(new GroupEnd(new BlockId(1), GroupOutcome.Errored)));
    }

    [Test]
    public void Group_CreateAsyncCancelled_ClosedCancelled()
    {
        Assert.CatchAsync<OperationCanceledException>(
            new Func<Task>(() => match.Groups.CreateAsync(() => Task.FromCanceled(new CancellationToken(true)))));

        Assert.That(match.Log.Entries[^1], Is.EqualTo(new GroupEnd(new BlockId(1), GroupOutcome.Cancelled)));
    }
}
