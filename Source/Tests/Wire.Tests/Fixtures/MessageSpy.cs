using VSA.Contracts;
using VSA.Handlers;

namespace Wire.Tests.Fixtures;

// A hook on a supertype of each primitive, for the B6 check. The host does not scan test assemblies.
public sealed class MessageSpy : After<IMessage>
{
    public override Task AfterAsync(IEnvelope<IMessage> envelope)
    {
        return Task.CompletedTask;
    }
}
