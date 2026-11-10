using Ecs.Contracts.Commands;
using Ecs.Domain;
using Microsoft.Extensions.DependencyInjection;
using Resources.Domain.Shared;
using VSA.Contracts;
using VSA.Contracts.Commands;
using VSA.Handlers.Commands;

namespace Resources.Slices.ChangeResource;

public sealed class ChangeResourceHandler : CommandHandler<ChangeResourceCommand, int>
{
    private readonly ICommandSender commands;

    public ChangeResourceHandler(ICommandSender commands)
    {
        this.commands = commands;
    }

    public override async Task<Result<int>> HandleAsync(Envelope<ChangeResourceCommand> envelope)
    {
        IWorldReader world = envelope.Services.GetRequiredService<IWorldReader>();
        ChangeResourceCommand change = envelope.Message;
        if (!world.TryById(change.Target, out Entity target))
        {
            return PrimitiveErrors.EntityMissing;
        }

        world.TryGet(target, out Health current);
        Health next = new Health { Value = current.Value + change.Delta };
        ChangeComponent<Health> write = new ChangeComponent<Health>(change.Target, next);
        Result<Health> written = await commands.SendAsync(write, envelope.Cancellation);
        return written.IsSuccess ? next.Value : written.Error!;
    }
}
