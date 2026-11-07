namespace Pipeline.Slices.Run;

public sealed class CommandRejectedException : InvalidOperationException
{
    public CommandRejectedException(Envelope envelope)
        : base($"{envelope.Message.GetType().Name} was rejected: {envelope.Reason}")
    {
        Envelope = envelope;
    }

    public Envelope Envelope { get; }
}
