namespace Framework.Pipeline
{
    public class Result
    {
        protected Result(EnvelopeState state, string reason)
        {
            State = state;
            Reason = reason;
        }

        public EnvelopeState State { get; }
        public string Reason { get; }
        public bool Accepted => State == EnvelopeState.Accepted;

        public static Result From(Envelope e) => new Result(e.State, e.Reason);
    }

    public sealed class Result<T> : Result
    {
        Result(EnvelopeState state, string reason, T value) : base(state, reason)
        {
            Value = value;
        }

        public T Value { get; }

        public static new Result<T> From(Envelope e)
            => new Result<T>(e.State, e.Reason, e.State == EnvelopeState.Accepted && e.Result is T value ? value : default);
    }
}
