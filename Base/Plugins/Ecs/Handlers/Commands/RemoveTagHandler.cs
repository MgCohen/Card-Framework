using Ecs.Contracts.Commands;
using Ecs.Domain.Shared;
using Ecs.Domain;
using Ecs.Infrastructure;
using VSA.Contracts;

namespace Ecs.Handlers.Commands;

public sealed class RemoveTagHandler<T> : PrimitiveHandler<RemoveTag<T>, Unit>
    where T : struct, ITag
{
    private readonly IWorldWriter writer;

    public RemoveTagHandler(MatchWorld match)
    {
        writer = match.Writer();
    }

    public override Task<Result<Unit>> HandleAsync(Envelope<RemoveTag<T>> envelope)
    {
        if (!writer.TryById(envelope.Message.Entity, out Entity entity))
        {
            return Task.FromResult<Result<Unit>>(PrimitiveErrors.EntityMissing);
        }

        return Task.FromResult(writer.RemoveTag<T>(entity) ? (Result<Unit>)Unit.Value : PrimitiveErrors.Unchanged);
    }
}
