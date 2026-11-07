using Pipeline.Domain.Shared;
using VSA.Contracts;

namespace Pipeline.Slices.Run;

public sealed class Envelope
{
    internal Envelope(MessageId id, IMessage message, Envelope? parent)
    {
        Id = id;
        Message = message;
        Parent = parent;
    }

    public MessageId Id { get; }

    public Envelope? Parent { get; }

    public IMessage Message { get; }

    public EnvelopeState State { get; private set; } = EnvelopeState.Listed;

    public string? Reason { get; private set; }

    public object? Result { get; private set; }

    internal void Start()
    {
        State = EnvelopeState.Running;
    }

    internal void Accept(object? result)
    {
        State = EnvelopeState.Accepted;
        Result = result;
    }

    internal void Reject(string? reason)
    {
        State = EnvelopeState.Rejected;
        Reason = reason;
    }

    internal void Fault()
    {
        State = EnvelopeState.Faulted;
    }
}
