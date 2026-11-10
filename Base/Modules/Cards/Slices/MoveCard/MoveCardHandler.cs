using VSA.Contracts;
using VSA.Contracts.Commands;
using VSA.Handlers.Commands;

namespace Cards.Slices.MoveCard;

public sealed class MoveCardHandler : CommandHandler<MoveCardCommand, Unit>
{
    private readonly ICommandSender commands;

    public MoveCardHandler(ICommandSender commands)
    {
        this.commands = commands;
    }

    public override Task<Result<Unit>> HandleAsync(Envelope<MoveCardCommand> envelope)
    {
        throw new NotImplementedException();
    }
}
