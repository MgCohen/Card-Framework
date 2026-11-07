namespace Pipeline.Slices.Run;

internal interface IPipelineHandler
{
    Type ScopeType { get; }

    Task<object?> RunAsync(object command, PipelineScope scope, CancellationToken ct);
}
