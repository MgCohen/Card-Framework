namespace Framework.States
{
    public sealed class StateStore : IReadOnlyStateStore
    {
        public GameState Current { get; private set; } = new MatchNotStartedState();

        // Sole writer: StatePushHandler only.
        internal void Set(GameState state) => Current = state;
    }
}
