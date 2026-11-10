using Ecs.Contracts.Commands;
using Ecs.Domain;
using Ecs.Domain.Shared;
using VSA.Contracts;

namespace Ecs.Handlers.Commands;

public sealed class RemoveComponentHandler<T> : PrimitiveHandler<RemoveComponent<T>, Unit>
    where T : unmanaged, IComponent
{
    private readonly IWorldWriter writer;

    public RemoveComponentHandler(MatchWorld match)
    {
        writer = match.Writer;
    }

    public override Task<Result<Unit>> HandleAsync(Envelope<RemoveComponent<T>> envelope)
    {
        if (!writer.TryById(envelope.Message.Entity, out Entity entity))
        {
            return Task.FromResult<Result<Unit>>(PrimitiveErrors.EntityMissing);
        }

        return Task.FromResult(writer.Remove<T>(entity) ? (Result<Unit>)Unit.Value : PrimitiveErrors.Unchanged);
    }
}
