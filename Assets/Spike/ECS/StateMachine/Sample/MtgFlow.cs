#nullable enable
using StateMachine.Engine;

namespace StateMachine.Sample
{
    /// <summary>
    /// Successor map. Linear MTG turn structure; loops Upkeep → ... → End →
    /// Upkeep. MatchNotStartedState kicks the cycle into Upkeep on first
    /// PassCommand.
    /// </summary>
    public sealed class SimplifiedMtgFlow : IStateFlow
    {
        public GameState? NextAfter(GameState current, IStateScope scope) => current switch
        {
            MatchNotStartedState  => new UpkeepState(),
            UpkeepState           => new Main1State(),
            Main1State            => new DeclareAttackersState(),
            DeclareAttackersState => new DeclareBlockersState(),
            DeclareBlockersState  => new CombatDamageState(),
            CombatDamageState     => new Main2State(),
            Main2State            => new EndState(),
            EndState              => new UpkeepState(),
            MatchEndedState       => null,
            _                     => null
        };
    }
}
