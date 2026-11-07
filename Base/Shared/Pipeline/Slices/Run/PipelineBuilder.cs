using VSA.Contracts;

namespace Pipeline.Slices.Run;

public sealed class PipelineBuilder<TMessage, TScope>
    where TMessage : class, IMessage
    where TScope : PipelineScope
{
    internal PipelineBuilder()
    {
    }

    internal Func<Envelope, PipelineServices, TScope>? CreateScope { get; private set; }

    internal Func<IReadOnlyList<Envelope>, Envelope>? SelectNext { get; private set; }

    internal Func<Envelope, TScope, CancellationToken, Task>? BeforeStep { get; private set; }

    internal Func<Envelope, TScope, CancellationToken, Task>? AfterStep { get; private set; }

    public PipelineBuilder<TMessage, TScope> Scope(Func<Envelope, PipelineServices, TScope> create)
    {
        CreateScope = create ?? throw new ArgumentNullException(nameof(create));
        return this;
    }

    public PipelineBuilder<TMessage, TScope> Select(Func<IReadOnlyList<Envelope>, Envelope> next)
    {
        SelectNext = next ?? throw new ArgumentNullException(nameof(next));
        return this;
    }

    public PipelineBuilder<TMessage, TScope> Before(Func<Envelope, TScope, CancellationToken, Task> step)
    {
        BeforeStep = step ?? throw new ArgumentNullException(nameof(step));
        return this;
    }

    public PipelineBuilder<TMessage, TScope> After(Func<Envelope, TScope, CancellationToken, Task> step)
    {
        AfterStep = step ?? throw new ArgumentNullException(nameof(step));
        return this;
    }
}
