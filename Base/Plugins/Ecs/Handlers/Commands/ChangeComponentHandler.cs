using Ecs.Contracts.Commands;
using Ecs.Domain.Shared;
using Ecs.Domain;
using Ecs.Infrastructure;
using VSA.Contracts;

namespace Ecs.Handlers.Commands;

public sealed class ChangeComponentHandler<T> : PrimitiveHandler<ChangeComponent<T>, T>
    where T : unmanaged, IComponent
{
    private readonly IWorldWriter writer;

    public ChangeComponentHandler(MatchWorld match)
    {
        writer = match.Writer();
    }

    public override Task<Result<T>> HandleAsync(Envelope<ChangeComponent<T>> envelope)
    {
        ChangeComponent<T> change = envelope.Message;
        if (!writer.TryById(change.Entity, out Entity entity))
        {
            return Task.FromResult<Result<T>>(PrimitiveErrors.EntityMissing);
        }

        return Task.FromResult(writer.Set(entity, change.Value) ? (Result<T>)change.Value : PrimitiveErrors.Unchanged);
    }
}
