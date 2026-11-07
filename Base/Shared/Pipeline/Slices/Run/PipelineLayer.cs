using VSA.Contracts;

namespace Pipeline.Slices.Run;

public abstract class PipelineLayer
{
    public abstract Type MessageType { get; }

    public static PipelineLayer<TMessage, TScope> Of<TMessage, TScope>(Action<PipelineBuilder<TMessage, TScope>> build)
        where TMessage : class, IMessage
        where TScope : PipelineScope
    {
        throw new NotImplementedException();
    }

    internal abstract Task<Envelope> RunMessageAsync(IMessage message, PipelineServices services, CancellationToken ct);
}
