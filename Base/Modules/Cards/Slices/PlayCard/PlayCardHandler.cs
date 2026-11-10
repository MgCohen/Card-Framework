using VSA.Contracts;
using VSA.Contracts.Commands;
using VSA.Handlers.Commands;

namespace Cards.Slices.PlayCard;

public sealed class PlayCardHandler : CommandHandler<PlayCardCommand, Unit>
{
    private readonly ICommandSender commands;

    public PlayCardHandler(ICommandSender commands)
    {
        this.commands = commands;
    }

    public override Task<Result<Unit>> HandleAsync(Envelope<PlayCardCommand> envelope)
    {
        throw new NotImplementedException();
    }
}
