using Ecs.Contracts.Commands;
using Ecs.Domain;
using Ecs.Domain.Shared;
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

    public override Task<Result<int>> HandleAsync(Envelope<ChangeResourceCommand> envelope)
    {
        return envelope.Message.Resource switch
        {
            Resource.Health => ChangeAsync<Health>(envelope),
            Resource.Mana => ChangeAsync<Mana>(envelope),
            _ => Task.FromResult<Result<int>>(ResourceErrors.UnknownResource),
        };
    }

    private async Task<Result<int>> ChangeAsync<T>(Envelope<ChangeResourceCommand> envelope)
        where T : unmanaged, IValue<int>
    {
        IWorldReader world = envelope.Services.GetRequiredService<IWorldReader>();
        ChangeResourceCommand change = envelope.Message;
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
