namespace Pipeline.Handlers.Commands;

public sealed class Result<T> : Result
{
    internal Result(EnvelopeState state, string? reason, T? value)
        : base(state, reason)
    {
        Value = value;
    }

    public T? Value { get; }
}
