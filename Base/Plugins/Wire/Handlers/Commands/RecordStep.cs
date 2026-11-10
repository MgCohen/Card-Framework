using Ecs.Contracts.Commands;
using VSA.Contracts;
using VSA.Handlers;

namespace Wire.Handlers.Commands;

public sealed class RecordStep : AfterStep<IPrimitive>
{
    private readonly WireLog log;

    public RecordStep(WireLog log)
    {
        this.log = log;
    }

    public override Task AfterAsync(IEnvelope<IPrimitive> envelope)
    {
        throw new NotImplementedException();
    }
}
