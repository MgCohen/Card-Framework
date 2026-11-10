using Ecs.Contracts.Commands;
using Ecs.Domain;
using VSA.Contracts;
using VSA.Handlers;

namespace Ecs.Handlers.Commands;

public sealed class CreateEntityId : BeforeStep<CreateEntity>
{
    private readonly World world;

    public CreateEntityId(World world)
    {
        this.world = world;
    }

    public override Task<CreateEntity> BeforeAsync(IEnvelope<CreateEntity> envelope)
    {
        throw new NotImplementedException();
    }
}
