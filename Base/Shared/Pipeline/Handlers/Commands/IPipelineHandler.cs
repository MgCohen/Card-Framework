namespace Pipeline.Handlers.Commands;

internal interface IPipelineHandler
{
    Type ScopeType { get; }

    Task<object?> RunAsync(object command, PipelineScope scope, CancellationToken ct);
}
