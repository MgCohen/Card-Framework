using Ecs.Contracts.Commands;
using VSA.Contracts;
using VSA.Handlers;

namespace Wire.Tests.Fixtures;

// A hook on a primitive, for the B6 check. The host does not scan test assemblies, so it never runs.
public sealed class HealthSpy : After<ChangeComponent<Health>>
{
    public override Task AfterAsync(IEnvelope<ChangeComponent<Health>> envelope)
    {
        return Task.CompletedTask;
    }
}
