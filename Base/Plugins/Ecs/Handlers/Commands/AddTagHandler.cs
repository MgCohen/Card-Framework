using Ecs.Contracts.Commands;
using Ecs.Domain;
using Ecs.Domain.Shared;
using VSA.Contracts;

namespace Ecs.Handlers.Commands;

public sealed class AddTagHandler<T> : PrimitiveHandler<AddTag<T>, Unit>
    where T : struct, ITag
{
    private readonly IWorldWriter writer;

    public AddTagHandler(IWorldWriter writer)
    {
        this.writer = writer;
    }

    public override Task<Result<Unit>> HandleAsync(Envelope<AddTag<T>> envelope)
    {
        if (!writer.TryById(envelope.Message.Entity, out Entity entity))
        {
            return Task.FromResult<Result<Unit>>(PrimitiveErrors.EntityMissing);
        }

        return Task.FromResult(writer.AddTag<T>(entity) ? (Result<Unit>)Unit.Value : PrimitiveErrors.Unchanged);
    }
}
