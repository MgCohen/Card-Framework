using Ecs.Contracts.Commands;
using Ecs.Domain.Shared;
using VSA.Contracts;
using VSA.Handlers;

namespace Wire.Tests.Fixtures;

// An open hook on a primitive, for the B6 check. In a module, the scan closes it for each component key.
public sealed class OpenHealthSpy<T> : After<ChangeComponent<T>>
    where T : unmanaged, IComponent
{
    public override Task AfterAsync(IEnvelope<ChangeComponent<T>> envelope)
    {
        return Task.CompletedTask;
    }
}
