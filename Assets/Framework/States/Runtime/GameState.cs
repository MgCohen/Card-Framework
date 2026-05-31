namespace Framework.States
{
    // A position in the game's FSM ("Playing", "Combat", "Mulligan"). Zero-data: identity
    // is the type, not any field. Same-kind comparison is `is`; structural equality is
    // trivially true (no fields). No payload travels on the wire because the record carries
    // none — the StatePushEvent<TState> channel discriminates by type.
    //
    // The behavior surface (Actions / OnEnter / OnExit / IsCompleted) lands in step 5c with
    // the command/effect pipeline those methods need.
    public abstract record GameState;

    // Framework baselines. The simplest game runs NotStarted -> Playing -> Ended as-is;
    // phase-structured games author additional GameState records of their own.
    public sealed record MatchNotStartedState : GameState;
    public sealed record PlayingState         : GameState;
    public sealed record MatchEndedState      : GameState;
}
