using Ecs.Contracts.Commands;
using Ecs.Domain;
using VSA.Contracts;

namespace Ecs.Handlers.Commands;

public sealed class DeleteEntityHandler : PrimitiveHandler<DeleteEntity, Unit>
{
    private readonly IWorldWriter writer;

    public DeleteEntityHandler(IWorldWriter writer)
    {
        this.writer = writer;
    }

    public override Task<Result<Unit>> HandleAsync(Envelope<DeleteEntity> envelope)
    {
        if (!writer.TryById(envelope.Message.Entity, out Entity entity))
        {
            return Task.FromResult<Result<Unit>>(PrimitiveErrors.EntityMissing);
        }

        writer.Delete(entity);
        return Task.FromResult<Result<Unit>>(Unit.Value);
    }
}
