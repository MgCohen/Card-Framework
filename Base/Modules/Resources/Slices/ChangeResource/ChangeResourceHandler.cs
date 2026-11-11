using Ecs.Contracts.Commands;
using Ecs.Domain;
using Ecs.Domain.Shared;
using Ecs.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using VSA.Contracts;
using VSA.Contracts.Commands;
using VSA.Handlers.Commands;

namespace Resources.Slices.ChangeResource;

public sealed class ChangeResourceHandler<T> : CommandHandler<ChangeResourceCommand<T>, int>
    where T : unmanaged, IValue<int>
{
    private readonly ICommandSender commands;

    public ChangeResourceHandler(ICommandSender commands)
    {
        this.commands = commands;
    }

    public override async Task<Result<int>> HandleAsync(Envelope<ChangeResourceCommand<T>> envelope)
    {
        IWorldReader world = envelope.Services.GetRequiredService<IWorldReader>();
        ChangeResourceCommand<T> change = envelope.Message;
        if (!world.TryById(change.Target, out Entity target))
        {
            return PrimitiveErrors.EntityMissing;
        }

        if (change.Delta == 0)
        {
            return PrimitiveErrors.Unchanged;
        }

        int current = world.TryGet(target, out T value) ? value.Value : 0;
        T next = new T { Value = current + change.Delta };
        ChangeComponent<T> write = new ChangeComponent<T>(change.Target, next);
        Result<T> written = await commands.SendAsync(write, envelope.Cancellation);
        return written.IsSuccess ? next.Value : written.Error!;
    }
}
