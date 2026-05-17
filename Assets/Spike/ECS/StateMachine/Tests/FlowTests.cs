#nullable enable
using NUnit.Framework;
using StateMachine.Engine;
using StateMachine.Sample;

namespace StateMachine.Tests
{
    /// <summary>
    /// Spike §v6: successor knowledge lives in IStateFlow, not on states.
    /// </summary>
    public class FlowTests
    {
        [Test]
        public void Flow_LinearChain_MatchesSpikeSpec()
        {
            var flow = new SimplifiedMtgFlow();
            var (engine, _) = MtgBootstrap.Build();
            var scope = new TestScope(engine.World, new MatchNotStartedState());

            Assert.IsInstanceOf<UpkeepState>          (flow.NextAfter(new MatchNotStartedState(),  scope));
            Assert.IsInstanceOf<Main1State>           (flow.NextAfter(new UpkeepState(),           scope));
            Assert.IsInstanceOf<DeclareAttackersState>(flow.NextAfter(new Main1State(),            scope));
            Assert.IsInstanceOf<DeclareBlockersState> (flow.NextAfter(new DeclareAttackersState(), scope));
            Assert.IsInstanceOf<CombatDamageState>    (flow.NextAfter(new DeclareBlockersState(),  scope));
            Assert.IsInstanceOf<Main2State>           (flow.NextAfter(new CombatDamageState(),     scope));
            Assert.IsInstanceOf<EndState>             (flow.NextAfter(new Main2State(),            scope));
            Assert.IsInstanceOf<UpkeepState>          (flow.NextAfter(new EndState(),              scope));
        }

        [Test]
        public void FullTurn_EndState_LoopsBackToUpkeep_AndMutatesMatchSingleton()
        {
            var (engine, state) = MtgBootstrap.Build();
            var atk = MtgBootstrap.SpawnCreature(engine.World, PlayerId.Player1, power: 1, toughness: 1);

            // Pump a clean turn: NotStarted → Upkeep → Main1 → DA → DB → CD → Main2 → End → Upkeep.
            engine.Enqueue(new PassCommand());           // → Upkeep
            engine.Enqueue(new PassCommand());           // → Main1
            engine.Enqueue(new PassCommand());           // → DeclareAttackers
            engine.Enqueue(new ToggleAttackerCommand(atk.Id));
            engine.Enqueue(new CommitAttackersCommand());// → DeclareBlockers
            engine.Enqueue(new CommitBlockersCommand()); // → CombatDamage → drains → Main2
            engine.Enqueue(new PassCommand());           // → End (drains Clear*, IncrementTurn, SwapActive)
            engine.Enqueue(new PassCommand());           // → Upkeep

            Assert.IsInstanceOf<UpkeepState>(state.Current);
            Assert.AreEqual(2, engine.World.Match.Turn);
            Assert.AreEqual(PlayerId.Player2, engine.World.Match.ActivePlayer);

            // EndState.OnEnter cleared the combat tags — atk is no longer attacking.
            Assert.IsFalse(atk.Has(MtgTags.IsAttacking));
            Assert.IsFalse(atk.Has(MtgTags.DamageResolved));
        }

        private sealed class TestScope : IStateScope
        {
            public World     World   { get; }
            public GameState Current { get; }
            public TestScope(World w, GameState c) { World = w; Current = c; }
        }
    }
}
