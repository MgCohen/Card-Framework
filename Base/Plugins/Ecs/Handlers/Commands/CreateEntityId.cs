using Ecs.Contracts.Commands;
using Ecs.Domain;
using VSA.Contracts;
using VSA.Handlers;

namespace Ecs.Handlers.Commands;

public sealed class CreateEntityId : BeforeStep<CreateEntity>
{
    private readonly IWorldWriter world;

    public CreateEntityId(MatchWorld match)
    {
        world = match.Writer;
    }

    public override Task<CreateEntity> BeforeAsync(IEnvelope<CreateEntity> envelope)
    {
        CreateEntity create = envelope.Message;
        return Task.FromResult(create.Entity == default ? create with { Entity = world.NextId() } : create);
    }
}
