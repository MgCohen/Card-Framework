using Cards.Domain.Shared;
using Ecs.Contracts.Commands;
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

    public override async Task<Result<EntityId>> HandleAsync(Envelope<CreateCardCommand> envelope)
    {
        CancellationToken ct = envelope.Cancellation;
        Result<EntityId> created = await commands.SendAsync(new CreateEntity(default), ct);
        if (!created.IsSuccess)
        {
            return created.Error!;
        }

        EntityId card = created.Value;
        Result<Unit> tagged = await commands.SendAsync(new AddTag<Card>(card), ct);
        if (!tagged.IsSuccess)
        {
            return tagged.Error!;
        }

        CardZone zone = new CardZone { Value = envelope.Message.Zone };
        Result<CardZone> placed = await commands.SendAsync(new ChangeComponent<CardZone>(card, zone), ct);
        return placed.IsSuccess ? card : placed.Error!;
    }
}
