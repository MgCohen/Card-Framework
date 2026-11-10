using Ecs.Contracts.Commands;
using Ecs.Domain;
using Ecs.Domain.Shared;
using VSA.Contracts;

namespace Ecs.Handlers.Commands;

public sealed class ChangeComponentHandler<T> : PrimitiveHandler<ChangeComponent<T>, T>
    where T : unmanaged, IComponent
{
    private readonly IWorldWriter writer;

    public ChangeComponentHandler(IWorldWriter writer)
    {
        this.writer = writer;
    }

    public override Task<Result<T>> HandleAsync(Envelope<ChangeComponent<T>> envelope)
    {
        throw new NotImplementedException();
    }
}
