namespace Framework.States
{
    // The FSM cursor — a Cell (the cardinality-1 Store). Same shape both sides; reconstructable
    // purely from the wire. StatePushHandler is the sole writer (server: after StateService.Push
    // emits; client: after the wire delivers) — no emit, no event awareness here. Initialized to
    // MatchNotStartedState so readers always have a cursor before the first push.
    public sealed class StateStore : IReadOnlyStateStore
    {
        public GameState Current { get; private set; } = new MatchNotStartedState();

        internal void Set(GameState state) => Current = state;
    }
}
