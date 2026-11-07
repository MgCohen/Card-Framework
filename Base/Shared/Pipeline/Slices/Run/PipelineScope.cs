using Pipeline.Domain.Shared;

namespace Pipeline.Slices.Run;

public abstract class PipelineScope
{
    public MessageId Id => throw new NotImplementedException();

    public bool IsClosed => throw new NotImplementedException();

    internal void Open(Envelope envelope)
    {
        throw new NotImplementedException();
    }

    internal void Close()
    {
        throw new NotImplementedException();
    }

    protected void ThrowIfClosed()
    {
        throw new NotImplementedException();
    }
}
