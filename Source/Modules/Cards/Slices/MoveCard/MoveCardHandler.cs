using Cards.Domain.Shared;
using Ecs.Contracts.Commands;
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

    public override async Task<Result<Unit>> HandleAsync(Envelope<MoveCardCommand> envelope)
    {
        MoveCardCommand move = envelope.Message;
        ChangeComponent<CardZone> change = new ChangeComponent<CardZone>(move.Card, new CardZone { Value = move.To });
        Result<CardZone> moved = await commands.SendAsync(change, envelope.Cancellation);
        return moved.IsSuccess ? Unit.Value : moved.Error!;
    }
}
