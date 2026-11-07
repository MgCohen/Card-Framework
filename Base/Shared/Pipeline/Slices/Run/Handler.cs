using VSA.Contracts.Commands;
using VSA.Handlers.Commands;

namespace Pipeline.Slices.Run;

public abstract class Handler<TCommand, TResult, TScope> : CommandHandler<TCommand, TResult>, IPipelineHandler
    where TCommand : ICommand<TResult>
    where TScope : PipelineScope
{
    Type IPipelineHandler.ScopeType => throw new NotImplementedException();

    public sealed override Task<TResult> HandleAsync(TCommand command, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    Task<object?> IPipelineHandler.RunAsync(object command, PipelineScope scope, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    protected abstract Task<TResult> HandleAsync(TCommand command, TScope scope, CancellationToken ct);
}
