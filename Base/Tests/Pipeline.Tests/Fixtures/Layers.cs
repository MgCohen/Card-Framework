using Pipeline.Slices.Run;
using VSA.Contracts;

namespace Pipeline.Tests;

public abstract record TestMessage : IMessage;

public abstract record OtherMessage : IMessage;

public interface IBothMessage : IMessage
{
}

public sealed class Log
{
    public List<string> Steps { get; } = new List<string>();

    public List<Envelope> Done { get; } = new List<Envelope>();
}

public sealed class RecordingWriter
{
    public List<string> Writes { get; } = new List<string>();

    public void Set(string value)
    {
        Writes.Add(value);
    }
}

public sealed class Mana
{
    public int Value { get; set; }
}

public sealed class TestScope : PipelineScope
{
    private readonly RecordingWriter writer;

    public TestScope(Log log, RecordingWriter writer)
    {
        Log = log;
        this.writer = writer;
    }

    public Log Log { get; }

    public RecordingWriter Writer
    {
        get
        {
            ThrowIfClosed();
            return writer;
        }
    }
}

public sealed class OtherScope : PipelineScope
{
    public OtherScope(Log log)
    {
        Log = log;
    }

    public Log Log { get; }
}

public static class Layers
{
    public static PipelineLayer<TestMessage, TestScope> Test()
    {
        return PipelineLayer.Of<TestMessage, TestScope>(layer => Steps(layer));
    }

    public static PipelineLayer<TestMessage, TestScope> Test(Action<PipelineBuilder<TestMessage, TestScope>> more)
    {
        return PipelineLayer.Of<TestMessage, TestScope>(layer => more(Steps(layer)));
    }

    public static PipelineLayer<OtherMessage, OtherScope> Other()
    {
        return PipelineLayer.Of<OtherMessage, OtherScope>(layer => layer
            .Scope((envelope, services) => new OtherScope(services.Require<Log>()))
            .After((envelope, scope, ct) =>
            {
                scope.Log.Done.Add(envelope);
                return Task.CompletedTask;
            }));
    }

    public static PipelineLayer<IBothMessage, OtherScope> Both()
    {
        return PipelineLayer.Of<IBothMessage, OtherScope>(layer => layer
            .Scope((envelope, services) => new OtherScope(services.Require<Log>())));
    }

    private static PipelineBuilder<TestMessage, TestScope> Steps(PipelineBuilder<TestMessage, TestScope> layer)
    {
        return layer
            .Scope((envelope, services) =>
            {
                Log log = services.Require<Log>();
                log.Steps.Add("scope");
                return new TestScope(log, services.Require<RecordingWriter>());
            })
            .Before((envelope, scope, ct) =>
            {
                scope.Log.Steps.Add("before");
                return Task.CompletedTask;
            })
            .After((envelope, scope, ct) =>
            {
                scope.Log.Steps.Add("after");
                scope.Log.Done.Add(envelope);
                return Task.CompletedTask;
            });
    }
}
