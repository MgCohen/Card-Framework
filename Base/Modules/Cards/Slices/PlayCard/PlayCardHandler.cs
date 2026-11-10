using Cards.Domain.Shared;
using Cards.Slices.MoveCard;
using Ecs.Domain;
using Microsoft.Extensions.DependencyInjection;
using Resources.Domain.Shared;
using Resources.Slices.ChangeResource;
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

    public override async Task<Result<Unit>> HandleAsync(Envelope<PlayCardCommand> envelope)
    {
        IWorldReader world = envelope.Services.GetRequiredService<IWorldReader>();
        PlayCardCommand play = envelope.Message;
        CancellationToken ct = envelope.Cancellation;
        int cost = world.TryGet(world.ById(play.Card), out Cost value) ? value.Value : 0;

        Result<Unit> moved = await commands.SendAsync(new MoveCardCommand(play.Card, Zone.Board), ct);
        if (!moved.IsSuccess || cost == 0)
        {
            return moved;
        }

        Result<int> paid = await commands.SendAsync(new ChangeResourceCommand(play.Player, -cost, Resource.Mana), ct);
        return paid.IsSuccess ? Unit.Value : paid.Error!;
    }
}
