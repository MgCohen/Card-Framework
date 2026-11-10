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
        throw new NotImplementedException();
    }
}
