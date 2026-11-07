using Pipeline.Slices.Run;

namespace Pipeline.Tests;

public sealed class PipelineLayerTests
{
    [Test]
    public void Of_WithNoScope_Throws()
    {
        Assert.Throws<InvalidOperationException>(new Action(() => PipelineLayer.Of<TestMessage, TestScope>(layer => { })));
    }

    [Test]
    public void MessageType_IsTheMessageBase()
    {
        Assert.That(Layers.Test().MessageType, Is.EqualTo(typeof(TestMessage)));
    }

    [Test]
    public void Add_GivesEachEnvelopeTheNextId()
    {
        PipelineLayer<TestMessage, TestScope> layer = Layers.Test();

        Envelope a = layer.Add(new Ping());
        Envelope b = layer.Add(new Ping());

        Assert.That(a.Id.Value, Is.EqualTo(1));
        Assert.That(b.Id.Value, Is.EqualTo(2));
        Assert.That(a.State, Is.EqualTo(EnvelopeState.Listed));
        Assert.That(layer.Items, Is.EqualTo(new[] { a, b }));
    }

    [Test]
    public void EachLayer_CountsItsOwnIds()
    {
        PipelineLayer<TestMessage, TestScope> test = Layers.Test();
        PipelineLayer<OtherMessage, OtherScope> other = Layers.Other();

        test.Add(new Ping());

        Assert.That(other.Add(new Count(1)).Id.Value, Is.EqualTo(1));
    }

    [Test]
    public async Task Steps_RunInFixedOrder()
    {
        PipelineLayer<TestMessage, TestScope> layer = Layers.Test();
        using Harness harness = new Harness(layer);

        Envelope e = await layer.RunAsync(new Ping(), harness.Services, CancellationToken.None);

        Assert.That(harness.Log.Steps, Is.EqualTo(new[] { "validate", "scope", "before", "handle", "after" }));
        Assert.That(e.State, Is.EqualTo(EnvelopeState.Accepted));
        Assert.That(e.Result, Is.EqualTo("pong"));
    }

    [Test]
    public async Task Reject_RunsNoOtherStep()
    {
        PipelineLayer<TestMessage, TestScope> layer = Layers.Test();
        using Harness harness = new Harness(layer);

        Envelope e = await layer.RunAsync(new Denied(), harness.Services, CancellationToken.None);

        Assert.That(harness.Log.Steps, Is.EqualTo(new[] { "validate" }));
        Assert.That(e.State, Is.EqualTo(EnvelopeState.Rejected));
        Assert.That(e.Reason, Is.EqualTo("no mana"));
    }

    [Test]
    public async Task Validate_ReadsThroughItsServices()
    {
        PipelineLayer<TestMessage, TestScope> layer = Layers.Test();
        using Harness harness = new Harness(layer);
        harness.Mana.Value = 1;

        Envelope e = await layer.RunAsync(new Denied(), harness.Services, CancellationToken.None);

        Assert.That(e.State, Is.EqualTo(EnvelopeState.Accepted));
        Assert.That(harness.Log.Steps, Does.Contain("handle"));
    }

    [Test]
    public async Task Fault_ClosesTheScope_AndSkipsAfter()
    {
        TestScope? opened = null;
        PipelineLayer<TestMessage, TestScope> layer = Layers.Test(more => more.Before((envelope, scope, ct) =>
        {
            opened = scope;
            return Task.CompletedTask;
        }));
        using Harness harness = new Harness(layer);
        Envelope e = layer.Add(new Boom());

        Exception? error = await Harness.Catch(() => layer.RunNextAsync(harness.Services, CancellationToken.None));

        Assert.That(error, Is.InstanceOf<InvalidOperationException>().With.Message.EqualTo("boom"));
        Assert.That(e.State, Is.EqualTo(EnvelopeState.Faulted));
        Assert.That(e.Result, Is.Null);
        Assert.That(opened!.IsClosed, Is.True);
        Assert.That(harness.Log.Steps, Has.No.Member("after"));
    }

    [Test]
    public async Task RunNext_TakesTheFirstByDefault()
    {
        PipelineLayer<TestMessage, TestScope> layer = Layers.Test();
        using Harness harness = new Harness(layer);
        Envelope first = layer.Add(new Ping());
        Envelope second = layer.Add(new Ping());

        Envelope? ran = await layer.RunNextAsync(harness.Services, CancellationToken.None);

        Assert.That(ran, Is.SameAs(first));
        Assert.That(first.State, Is.EqualTo(EnvelopeState.Accepted));
        Assert.That(second.State, Is.EqualTo(EnvelopeState.Listed));
        Assert.That(layer.Items, Is.EqualTo(new[] { second }));
    }

    [Test]
    public async Task Select_FromTheBuilder_PicksTheNext()
    {
        PipelineLayer<TestMessage, TestScope> layer = Layers.Test(more => more.Select(items => items[items.Count - 1]));
        using Harness harness = new Harness(layer);
        Envelope first = layer.Add(new Ping());
        Envelope second = layer.Add(new Ping());

        Assert.That(layer.TryPeek(out Envelope? next), Is.True);
        Assert.That(next, Is.SameAs(second));

        await layer.RunNextAsync(harness.Services, CancellationToken.None);

        Assert.That(second.State, Is.EqualTo(EnvelopeState.Accepted));
        Assert.That(first.State, Is.EqualTo(EnvelopeState.Listed));
    }

    [Test]
    public async Task RunNext_EmptyList_DoesNothing()
    {
        PipelineLayer<TestMessage, TestScope> layer = Layers.Test();
        using Harness harness = new Harness(layer);

        Envelope? ran = await layer.RunNextAsync(harness.Services, CancellationToken.None);

        Assert.That(ran, Is.Null);
        Assert.That(layer.TryPeek(out _), Is.False);
        Assert.That(harness.Log.Steps, Is.Empty);
    }

    [Test]
    public void TryTake_RemovesWithoutARun()
    {
        PipelineLayer<TestMessage, TestScope> layer = Layers.Test();
        Envelope e = layer.Add(new Ping());

        Assert.That(layer.TryTake(out Envelope? taken), Is.True);

        Assert.That(taken, Is.SameAs(e));
        Assert.That(e.State, Is.EqualTo(EnvelopeState.Listed));
        Assert.That(layer.Items, Is.Empty);
        Assert.That(layer.TryTake(out _), Is.False);
    }

    [Test]
    public void TryPeek_DoesNotRemove()
    {
        PipelineLayer<TestMessage, TestScope> layer = Layers.Test();
        Envelope e = layer.Add(new Ping());

        Assert.That(layer.TryPeek(out Envelope? next), Is.True);

        Assert.That(next, Is.SameAs(e));
        Assert.That(layer.Items, Is.EqualTo(new[] { e }));
    }

    [Test]
    public void Remove_Listed_ReturnsTrue()
    {
        PipelineLayer<TestMessage, TestScope> layer = Layers.Test();
        Envelope e = layer.Add(new Ping());

        Assert.That(layer.Remove(e.Id), Is.True);
        Assert.That(layer.Items, Is.Empty);
        Assert.That(layer.Remove(e.Id), Is.False);
    }

    [Test]
    public async Task Remove_Running_ReturnsFalse()
    {
        PipelineLayer<TestMessage, TestScope> layer = Layers.Test();
        using Harness harness = new Harness(layer);
        Envelope e = layer.Add(new Hold());

        Task<Envelope?> run = layer.RunNextAsync(harness.Services, CancellationToken.None);

        Assert.That(e.State, Is.EqualTo(EnvelopeState.Running));
        Assert.That(layer.Remove(e.Id), Is.False);
        Assert.That(run.IsCompleted, Is.False);

        harness.Hold.Release.SetResult(7);
        await run;
        Assert.That(e.State, Is.EqualTo(EnvelopeState.Accepted));
        Assert.That(e.Result, Is.EqualTo(7));
    }

    [Test]
    public async Task Handle_TypedResult_ReachesResultOfT()
    {
        PipelineLayer<TestMessage, TestScope> layer = Layers.Test();
        using Harness harness = new Harness(layer);

        Envelope e = await layer.RunAsync(new Echo(5), harness.Services, CancellationToken.None);

        Assert.That(Result.From<int>(e).Value, Is.EqualTo(5));
    }

    [Test]
    public async Task Handler_WritesThroughItsScope()
    {
        PipelineLayer<TestMessage, TestScope> layer = Layers.Test();
        using Harness harness = new Harness(layer);

        await layer.RunAsync(new Write("x"), harness.Services, CancellationToken.None);

        Assert.That(harness.Writer.Writes, Is.EqualTo(new[] { "x" }));
    }

    [Test]
    public async Task ScopeMember_AfterTheRun_Throws()
    {
        PipelineLayer<TestMessage, TestScope> layer = Layers.Test();
        using Harness harness = new Harness(layer);

        Envelope e = await layer.RunAsync(new Keep(), harness.Services, CancellationToken.None);

        TestScope kept = harness.Keep.Kept!;
        Assert.That(kept.Id, Is.EqualTo(e.Id));
        Assert.That(kept.IsClosed, Is.True);
        Assert.Throws<InvalidOperationException>(new Action(() => kept.Writer.Set("late")));
    }

    [Test]
    public async Task Validate_Throws_Faults()
    {
        PipelineLayer<TestMessage, TestScope> layer = Layers.Test();
        using Harness harness = new Harness(layer);
        Envelope e = layer.Add(new Crash());

        Exception? error = await Harness.Catch(() => layer.RunNextAsync(harness.Services, CancellationToken.None));

        Assert.That(error, Is.InstanceOf<InvalidOperationException>().With.Message.EqualTo("crash"));
        Assert.That(e.State, Is.EqualTo(EnvelopeState.Faulted));
        Assert.That(harness.Log.Steps, Is.EqualTo(new[] { "validate" }));
    }

    [Test]
    public async Task Scope_Throws_Faults()
    {
        PipelineLayer<TestMessage, TestScope> layer = PipelineLayer.Of<TestMessage, TestScope>(builder => builder
            .Scope((envelope, services) => throw new InvalidOperationException("scope")));
        using Harness harness = new Harness(layer);
        Envelope e = layer.Add(new Ping());

        Exception? error = await Harness.Catch(() => layer.RunNextAsync(harness.Services, CancellationToken.None));

        Assert.That(error, Is.InstanceOf<InvalidOperationException>().With.Message.EqualTo("scope"));
        Assert.That(e.State, Is.EqualTo(EnvelopeState.Faulted));
        Assert.That(harness.Log.Steps, Has.No.Member("handle"));
    }

    [Test]
    public async Task Scope_ReturnsNull_Faults()
    {
        PipelineLayer<TestMessage, TestScope> layer = PipelineLayer.Of<TestMessage, TestScope>(builder => builder
            .Scope((envelope, services) => null!));
        using Harness harness = new Harness(layer);
        Envelope e = layer.Add(new Ping());

        Exception? error = await Harness.Catch(() => layer.RunNextAsync(harness.Services, CancellationToken.None));

        Assert.That(error, Is.InstanceOf<InvalidOperationException>().With.Message.Contains("Scope"));
        Assert.That(e.State, Is.EqualTo(EnvelopeState.Faulted));
    }

    [Test]
    public async Task Before_Throws_FaultsAndClosesTheScope()
    {
        TestScope? opened = null;
        PipelineLayer<TestMessage, TestScope> layer = PipelineLayer.Of<TestMessage, TestScope>(builder => builder
            .Scope((envelope, services) => new TestScope(services.Require<Log>(), services.Require<RecordingWriter>()))
            .Before((envelope, scope, ct) =>
            {
                opened = scope;
                throw new InvalidOperationException("before");
            }));
        using Harness harness = new Harness(layer);
        Envelope e = layer.Add(new Ping());

        Exception? error = await Harness.Catch(() => layer.RunNextAsync(harness.Services, CancellationToken.None));

        Assert.That(error, Is.InstanceOf<InvalidOperationException>().With.Message.EqualTo("before"));
        Assert.That(e.State, Is.EqualTo(EnvelopeState.Faulted));
        Assert.That(opened!.IsClosed, Is.True);
        Assert.That(harness.Log.Steps, Has.No.Member("handle"));
    }

    [Test]
    public async Task Scope_ReturnsAUsedScope_Faults()
    {
        TestScope? shared = null;
        PipelineLayer<TestMessage, TestScope> layer = PipelineLayer.Of<TestMessage, TestScope>(builder => builder
            .Scope((envelope, services) =>
            {
                shared ??= new TestScope(services.Require<Log>(), services.Require<RecordingWriter>());
                return shared;
            }));
        using Harness harness = new Harness(layer);
        await layer.RunAsync(new Ping(), harness.Services, CancellationToken.None);

        Exception? error = await Harness.Catch(
            () => layer.RunAsync(new Ping(), harness.Services, CancellationToken.None));

        Assert.That(error, Is.InstanceOf<InvalidOperationException>().With.Message.Contains("new scope"));
    }

    [Test]
    public void Select_ReturnsAnEnvelopeOfNoList_Throws()
    {
        PipelineLayer<TestMessage, TestScope> other = Layers.Test();
        Envelope foreign = other.Add(new Ping());
        PipelineLayer<TestMessage, TestScope> layer = Layers.Test(more => more.Select(items => foreign));
        layer.Add(new Ping());

        Assert.Throws<InvalidOperationException>(new Action(() => layer.TryPeek(out _)));
    }

    [Test]
    public void Items_IsASnapshot()
    {
        PipelineLayer<TestMessage, TestScope> layer = Layers.Test();
        layer.Add(new Ping());
        IReadOnlyList<Envelope> before = layer.Items;

        layer.Add(new Ping());

        Assert.That(before, Has.Count.EqualTo(1));
        Assert.That(layer.Items, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task CommandWithTwoResultTypes_Faults()
    {
        await AssertFaults(new Twice(), "ICommand<T>");
    }

    [Test]
    public async Task NoHandler_Faults()
    {
        await AssertFaults(new Orphan(), "no handler");
    }

    [Test]
    public async Task PlainCommandHandler_ForALayerMessage_Faults()
    {
        await AssertFaults(new Loose(), "Handler<");
    }

    [Test]
    public async Task HandlerScope_IsNotTheLayerScope_Faults()
    {
        await AssertFaults(new Misfit(), nameof(OtherScope));
    }

    private static async Task AssertFaults(TestMessage message, string text)
    {
        PipelineLayer<TestMessage, TestScope> layer = Layers.Test();
        using Harness harness = new Harness(layer);
        Envelope e = layer.Add(message);

        Exception? error = await Harness.Catch(() => layer.RunNextAsync(harness.Services, CancellationToken.None));

        Assert.That(error, Is.InstanceOf<InvalidOperationException>().With.Message.Contains(text));
        Assert.That(e.State, Is.EqualTo(EnvelopeState.Faulted));
        Assert.That(harness.Log.Steps, Has.No.Member("before"));
    }
}
