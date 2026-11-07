using Pipeline.Domain.Shared;
using VSA.Contracts;

namespace Pipeline.Slices.Run;

public sealed class PipelineLayer<TMessage, TScope> : PipelineLayer
    where TMessage : class, IMessage
    where TScope : PipelineScope
{
    internal PipelineLayer()
    {
    }

    public override Type MessageType => typeof(TMessage);

    public IReadOnlyList<Envelope> Items => throw new NotImplementedException();

    public Envelope Add(TMessage message)
    {
        throw new NotImplementedException();
    }

    public bool Remove(MessageId id)
    {
        throw new NotImplementedException();
    }

    public bool TryPeek(out Envelope? envelope)
    {
        throw new NotImplementedException();
    }

    public bool TryTake(out Envelope? envelope)
    {
        throw new NotImplementedException();
    }

    public Task<Envelope?> RunNextAsync(PipelineServices services, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<Envelope> RunAsync(TMessage message, PipelineServices services, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    internal override Task<Envelope> RunMessageAsync(IMessage message, PipelineServices services, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
