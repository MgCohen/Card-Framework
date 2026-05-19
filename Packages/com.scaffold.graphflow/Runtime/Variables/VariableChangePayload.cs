namespace Scaffold.GraphFlow
{
    // Payload object passed through Flow when a handle Subscribe callback triggers an Observe.
    // Shape mirrors OnTrigger<TEvent> + GetPayload<TEvent>(): a plain class, no struct
    // boxing concerns since flow.GetPayload<T>() requires `where T : class`.
    public sealed class VariableChangePayload<T>
    {
        public readonly T Value;
        public VariableChangePayload(T value) => Value = value;
    }
}
