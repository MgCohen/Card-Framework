using VSA.Contracts;

namespace Pipeline.Slices.Run;

public sealed class PipelineBuilder<TMessage, TScope>
    where TMessage : class, IMessage
    where TScope : PipelineScope
{
    internal PipelineBuilder()
    {
    }

    public PipelineBuilder<TMessage, TScope> Scope(Func<Envelope, PipelineServices, TScope> create)
    {
        throw new NotImplementedException();
    }

    public PipelineBuilder<TMessage, TScope> Select(Func<IReadOnlyList<Envelope>, Envelope> next)
    {
        throw new NotImplementedException();
    }

    public PipelineBuilder<TMessage, TScope> Before(Func<Envelope, TScope, CancellationToken, Task> step)
    {
        throw new NotImplementedException();
    }

    public PipelineBuilder<TMessage, TScope> After(Func<Envelope, TScope, CancellationToken, Task> step)
    {
        throw new NotImplementedException();
    }
}
