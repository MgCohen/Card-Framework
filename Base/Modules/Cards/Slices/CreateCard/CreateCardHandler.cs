using Ecs.Domain.Shared;
using VSA.Contracts;
using VSA.Contracts.Commands;
using VSA.Handlers.Commands;

namespace Cards.Slices.CreateCard;

public sealed class CreateCardHandler : CommandHandler<CreateCardCommand, EntityId>
{
    private readonly ICommandSender commands;

    public CreateCardHandler(ICommandSender commands)
    {
        this.commands = commands;
    }

    public override Task<Result<EntityId>> HandleAsync(Envelope<CreateCardCommand> envelope)
    {
        throw new NotImplementedException();
    }
}
