#nullable enable
namespace StateMachine.Engine
{
    /// <summary>
    /// The "load-bearing" singleton from spike §v6 — holds game-wide state
    /// that won't live on a stateless GameState record: turn number, active
    /// player, outcome. Plain class for the spike; real shape (entity vs.
    /// service) is its own design pass.
    /// </summary>
    public sealed class MatchSingleton
    {
        public int      Turn         { get; set; } = 1;
        public PlayerId ActivePlayer { get; set; } = PlayerId.Player1;
        public PlayerId Defending    => ActivePlayer == PlayerId.Player1 ? PlayerId.Player2 : PlayerId.Player1;
        public Outcome  Outcome      { get; set; } = Outcome.Undecided;

        public void SwapActivePlayer() =>
            ActivePlayer = ActivePlayer == PlayerId.Player1 ? PlayerId.Player2 : PlayerId.Player1;
    }

    public enum PlayerId { Player1, Player2 }
    public enum Outcome  { Undecided, Player1Wins, Player2Wins, Draw }
}
