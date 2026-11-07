using VSA.Contracts.Commands;
using VSA.Handlers.Commands;

namespace Pipeline.Slices.Run;

public abstract class Handler<TCommand, TResult, TScope> : CommandHandler<TCommand, TResult>, IPipelineHandler
    where TCommand : ICommand<TResult>
    where TScope : PipelineScope
{
    Type IPipelineHandler.ScopeType => typeof(TScope);

    public sealed override Task<TResult> HandleAsync(TCommand command, CancellationToken ct)
    {
        throw new InvalidOperationException(
            $"{GetType().Name} runs only in its pipeline layer. Send {typeof(TCommand).Name} with ICommandSender.");
    }

    async Task<object?> IPipelineHandler.RunAsync(object command, PipelineScope scope, CancellationToken ct)
    {
        return await HandleAsync((TCommand)command, (TScope)scope, ct);
    }

    protected abstract Task<TResult> HandleAsync(TCommand command, TScope scope, CancellationToken ct);
}
