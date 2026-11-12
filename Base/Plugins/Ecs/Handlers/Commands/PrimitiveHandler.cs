using Ecs.Contracts.Commands;
using VSA.Analyzers.Markers;
using VSA.Contracts;
using VSA.Handlers;

namespace Ecs.Handlers.Commands;

[AutoRegister]
public abstract class PrimitiveHandler<TPrimitive, TResult> : IHandler<TPrimitive>
    where TPrimitive : class, IPrimitive<TResult>
{
    public abstract Task<Result<TResult>> HandleAsync(Envelope<TPrimitive> envelope);

    async Task<IMessageResult> IHandler.HandleAsync(Envelope envelope)
    {
        return await HandleAsync((Envelope<TPrimitive>)envelope);
    }
}
