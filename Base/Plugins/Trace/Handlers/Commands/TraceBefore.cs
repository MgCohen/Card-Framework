using VSA.Contracts;
using VSA.Handlers;

namespace Trace.Handlers.Commands;

public sealed class TraceBefore : Before<IMessage>
{
    private readonly TraceLog log;

    public TraceBefore(TraceLog log)
    {
        this.log = log;
    }

    public override int Order => int.MaxValue;

    public override Task<IMessage> BeforeAsync(IEnvelope<IMessage> envelope)
    {
        log.Add(new TraceEvent(
            TraceKind.Send, envelope.Id, envelope.Parent?.Id, envelope.Depth, envelope.Message, string.Empty));
        return Task.FromResult(envelope.Message);
    }
}
