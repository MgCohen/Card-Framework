using Pipeline.Domain.Shared;

namespace Pipeline.Slices.Run;

public abstract class PipelineScope
{
    private Envelope? envelope;

    public MessageId Id => envelope?.Id ?? throw new InvalidOperationException($"{GetType().Name} is not open.");

    public bool IsClosed { get; private set; }

    internal void Open(Envelope run)
    {
        envelope = run;
    }

    internal void Close()
    {
        IsClosed = true;
    }

    protected void ThrowIfClosed()
    {
        if (IsClosed)
        {
            throw new InvalidOperationException(
                $"{GetType().Name} is closed. A scope member works only during its run.");
        }
    }
}
