using Ecs.Contracts.Commands;
using Ecs.Domain;
using Ecs.Domain.Shared;
using VSA.Contracts;

namespace Ecs.Handlers.Commands;

public sealed class CreateEntityHandler : PrimitiveHandler<CreateEntity, EntityId>
{
    private readonly World world;

    public CreateEntityHandler(World world)
    {
        this.world = world;
    }

    public override Task<Result<EntityId>> HandleAsync(Envelope<CreateEntity> envelope)
    {
        EntityId id = envelope.Message.Entity;
        if (world.TryById(id, out _))
        {
            return Task.FromResult<Result<EntityId>>(PrimitiveErrors.EntityExists);
        }

        return Task.FromResult<Result<EntityId>>(world.Create(id).Id);
    }
}
