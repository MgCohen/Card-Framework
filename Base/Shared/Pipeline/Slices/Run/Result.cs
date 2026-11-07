namespace Pipeline.Slices.Run;

public class Result
{
    protected Result(EnvelopeState state, string? reason)
    {
        State = state;
        Reason = reason;
    }

    public EnvelopeState State { get; }

    public string? Reason { get; }

    public bool Accepted => State == EnvelopeState.Accepted;

    public static Result From(Envelope e)
    {
        ThrowIfNotDone(e);
        return new Result(e.State, e.Reason);
    }

    public static Result<T> From<T>(Envelope e)
    {
        ThrowIfNotDone(e);
        if (e.State == EnvelopeState.Rejected)
        {
            return new Result<T>(e.State, e.Reason, default);
        }

        if (e.Result is T value)
        {
            return new Result<T>(e.State, e.Reason, value);
        }

        if (e.Result == null && default(T) == null)
        {
            return new Result<T>(e.State, e.Reason, default);
        }

        throw new InvalidOperationException(
            $"Envelope {e.Id.Value} result is {e.Result?.GetType().Name ?? "null"}, not {typeof(T).Name}.");
    }

    private static void ThrowIfNotDone(Envelope e)
    {
        if (e.State != EnvelopeState.Accepted && e.State != EnvelopeState.Rejected)
        {
            throw new InvalidOperationException(
                $"Envelope {e.Id.Value} is {e.State}. A result needs Accepted or Rejected.");
        }
    }
}
