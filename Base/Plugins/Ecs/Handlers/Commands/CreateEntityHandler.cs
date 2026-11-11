using Ecs.Contracts.Commands;
using Ecs.Domain.Shared;
using Ecs.Domain;
using Ecs.Infrastructure;
using VSA.Contracts;

namespace Ecs.Handlers.Commands;

public sealed class CreateEntityHandler : PrimitiveHandler<CreateEntity, EntityId>
{
    private readonly IWorldWriter world;

    public CreateEntityHandler(MatchWorld match)
    {
        world = match.Writer();
    }

    public override Task<Result<EntityId>> HandleAsync(Envelope<CreateEntity> envelope)
    {
        EntityId id = envelope.Message.Entity;
        if (id.Value <= 0)
        {
            return Task.FromResult<Result<EntityId>>(PrimitiveErrors.InvalidId);
        }

        if (world.TryById(id, out _))
        {
            return Task.FromResult<Result<EntityId>>(PrimitiveErrors.EntityExists);
        }

        return Task.FromResult<Result<EntityId>>(world.Create(id).Id);
    }
}
