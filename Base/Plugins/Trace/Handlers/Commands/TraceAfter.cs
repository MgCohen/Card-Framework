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
        return Task.CompletedTask;
    }
}
