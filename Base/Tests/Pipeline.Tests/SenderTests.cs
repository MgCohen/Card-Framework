using Pipeline.Handlers.Commands;

namespace Pipeline.Tests;

public sealed class SenderTests
{
    [Test]
    public async Task Send_LayerCommand_ReturnsTheResult()
    {
        using Harness harness = new Harness(Layers.Test());

        string result = await harness.Sender.SendAsync(new Ping(), CancellationToken.None);

        Assert.That(result, Is.EqualTo("pong"));
        Assert.That(harness.Log.Steps, Is.EqualTo(new[] { "validate", "scope", "before", "handle", "after" }));
    }

    [Test]
    public async Task Send_PlainCommand_RunsTheVsaHandler()
    {
        using Harness harness = new Harness(Layers.Test());

        int result = await harness.Sender.SendAsync(new Plain(1), CancellationToken.None);

        Assert.That(result, Is.EqualTo(2));
        Assert.That(harness.Log.Steps, Is.Empty);
    }

    [Test]
    public async Task Send_Rejected_ThrowsCommandRejectedException()
    {
        using Harness harness = new Harness(Layers.Test());

        Exception? error = await Harness.Catch(() => harness.Sender.SendAsync(new Denied(), CancellationToken.None));

        Assert.That(error, Is.InstanceOf<CommandRejectedException>());
        Assert.That(((CommandRejectedException)error!).Envelope.Reason, Is.EqualTo("no mana"));
    }

    [Test]
    public async Task Send_Faulted_ThrowsTheHandlerException()
    {
        using Harness harness = new Harness(Layers.Test());

        Exception? error = await Harness.Catch(() => harness.Sender.SendAsync(new Boom(), CancellationToken.None));

        Assert.That(error, Is.InstanceOf<InvalidOperationException>().With.Message.EqualTo("boom"));
    }

    [Test]
    public async Task Send_FromNoRun_HasNoParent()
    {
        using Harness harness = new Harness(Layers.Test());

        await harness.Sender.SendAsync(new Ping(), CancellationToken.None);

        Assert.That(harness.Log.Done.Single().Parent, Is.Null);
    }

    [Test]
    public async Task Send_InARun_SetsTheParent()
    {
        using Harness harness = new Harness(Layers.Test());

        string result = await harness.Sender.SendAsync(new Nest(), CancellationToken.None);

        Envelope child = harness.Log.Done.Single(e => e.Message is Ping);
        Envelope parent = harness.Log.Done.Single(e => e.Message is Nest);
        Assert.That(result, Is.EqualTo("pong"));
        Assert.That(child.Parent, Is.SameAs(parent));
        Assert.That(parent.Parent, Is.Null);
    }

    [Test]
    public async Task Send_TwoChildren_BothHaveTheParent()
    {
        using Harness harness = new Harness(Layers.Test());

        await harness.Sender.SendAsync(new NestTwice(), CancellationToken.None);
        await harness.Sender.SendAsync(new Ping(), CancellationToken.None);

        Envelope parent = harness.Log.Done.Single(e => e.Message is NestTwice);
        Envelope[] pings = harness.Log.Done.Where(e => e.Message is Ping).ToArray();
        Assert.That(pings, Has.Length.EqualTo(3));
        Assert.That(pings[0].Parent, Is.SameAs(parent));
        Assert.That(pings[1].Parent, Is.SameAs(parent));
        Assert.That(pings[2].Parent, Is.Null);
    }

    [Test]
    public async Task Send_ToAnotherContainer_HasNoParent()
    {
        using Harness client = new Harness(Layers.Test());
        using Harness server = new Harness(Layers.Test(more => more.Before(async (envelope, scope, ct) =>
        {
            await client.Sender.SendAsync(new Ping(), ct);
        })));

        await server.Sender.SendAsync(new Ping(), CancellationToken.None);

        Assert.That(client.Log.Done.Single().Parent, Is.Null);
    }

    [Test]
    public async Task Send_PipelineHandlerWithNoLayer_Throws()
    {
        using Harness harness = new Harness();

        Exception? error = await Harness.Catch(() => harness.Sender.SendAsync(new Ping(), CancellationToken.None));

        Assert.That(error, Is.InstanceOf<InvalidOperationException>().With.Message.Contains("no layer"));
    }

    [Test]
    public async Task Send_FromOneLayer_RunsInTheOtherLayer()
    {
        using Harness harness = new Harness(Layers.Test(), Layers.Other());

        int result = await harness.Sender.SendAsync(new Cross(3), CancellationToken.None);

        Envelope count = harness.Log.Done.Single(e => e.Message is Count);
        Assert.That(result, Is.EqualTo(6));
        Assert.That(count.Parent!.Message, Is.InstanceOf<Cross>());
    }

    [Test]
    public void TwoLayers_WithOneMessageType_Throw()
    {
        using Harness harness = new Harness();
        PipelineLayer[] layers = { Layers.Test(), Layers.Test() };

        InvalidOperationException e = Assert.Throws<InvalidOperationException>(
            new Action(() => new PipelineCommandSender(harness.Services, layers)))!;

        Assert.That(e.Message, Does.Contain(nameof(TestMessage)));
    }

    [Test]
    public async Task Message_OfTwoLayers_Throws()
    {
        using Harness harness = new Harness(Layers.Test(), Layers.Both());

        Exception? error = await Harness.Catch(() => harness.Sender.SendAsync(new Both(), CancellationToken.None));

        Assert.That(error, Is.InstanceOf<InvalidOperationException>().With.Message.Contains(nameof(Both)));
    }

    [Test]
    public async Task VsaHandleAsync_OfAPipelineHandler_Throws()
    {
        PingHandler handler = new PingHandler();

        Exception? error = await Harness.Catch(() => handler.HandleAsync(new Ping(), CancellationToken.None));

        Assert.That(error, Is.InstanceOf<InvalidOperationException>());
    }
}
