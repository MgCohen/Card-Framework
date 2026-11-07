using VSA.Contracts;

namespace Pipeline.Slices.Run;

public abstract class PipelineLayer
{
    private static readonly AsyncLocal<Envelope?> current = new AsyncLocal<Envelope?>();

    public abstract Type MessageType { get; }

    internal static Envelope? CurrentRun
    {
        get => current.Value;
        set => current.Value = value;
    }

    public static PipelineLayer<TMessage, TScope> Of<TMessage, TScope>(Action<PipelineBuilder<TMessage, TScope>> build)
        where TMessage : class, IMessage
        where TScope : PipelineScope
    {
        ArgumentNullException.ThrowIfNull(build);
        PipelineBuilder<TMessage, TScope> builder = new PipelineBuilder<TMessage, TScope>();
        build(builder);
        if (builder.CreateScope == null)
        {
            throw new InvalidOperationException(
                $"The layer of {typeof(TMessage).Name} has no Scope. Call Scope on its builder.");
        }

        return new PipelineLayer<TMessage, TScope>(builder);
    }

    internal abstract Task<Envelope> RunMessageAsync(IMessage message, PipelineServices services, CancellationToken ct);
}
