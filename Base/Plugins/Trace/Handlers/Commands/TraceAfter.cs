using VSA.Contracts;
using VSA.Handlers;

namespace Trace.Handlers.Commands;

public sealed class TraceAfter : After<IMessage>
{
    private readonly TraceLog log;

    public TraceAfter(TraceLog log)
    {
        this.log = log;
    }

    public override int Order => int.MinValue;

    public override Task AfterAsync(IEnvelope<IMessage> envelope)
    {
        bool done = envelope.State is EnvelopeState.Accepted or EnvelopeState.Rejected or EnvelopeState.Faulted;
        if (log.Recording && done)
        {
            TraceKind kind = KindOf(envelope.State);
            log.Add(new TraceEvent(
                kind, envelope.Id, envelope.Parent?.Id, envelope.Depth, envelope.Message, Outcome(envelope)));
        }

        return Task.CompletedTask;
    }

    private static TraceKind KindOf(EnvelopeState state)
    {
        return state switch
        {
            EnvelopeState.Rejected => TraceKind.Reject,
            EnvelopeState.Faulted => TraceKind.Fault,
            _ => TraceKind.Result,
        };
    }

    private static string Outcome(IEnvelope<IMessage> envelope)
    {
        if (envelope.State == EnvelopeState.Faulted)
        {
            return (envelope as Envelope)?.Exception?.GetType().Name ?? "Exception";
        }

        if (envelope.Error is not null)
        {
            return $"Error({envelope.Error.Code})";
        }

        object? value = envelope.Result?.Value;
        return value is null or Unit ? "Ok" : $"Ok({TraceText.Value(value)})";
    }
}
