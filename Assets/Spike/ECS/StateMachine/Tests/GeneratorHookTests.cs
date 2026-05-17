#nullable enable
using System.Linq;
using NUnit.Framework;
using StateMachine.Engine;
using StateMachine.Sample;

namespace StateMachine.Tests
{
    /// <summary>
    /// Spike §v6 Q6: OnEnter/OnExit are *generators*. They return commands
    /// that the push handler enqueues. The commands themselves land as
    /// wire events. On replay, the events get walked — hooks never re-fire.
    /// </summary>
    public class GeneratorHookTests
    {
        [Test]
        public void UpkeepOnEnter_OutputsLandInTheQueue_AndFire()
        {
            var (engine, _) = MtgBootstrap.Build();
            engine.Enqueue(new PassCommand());

            var executed = engine.World.Log.OfType<CommandExecutedEvent>()
                                          .Select(e => e.CommandName).ToList();
            Assert.Contains("UntapAllCommand", executed);
            Assert.Contains("DrawCardCommand", executed);
        }

        [Test]
        public void UpkeepOnEnter_ReadsActivePlayer_FromMatchSingleton()
        {
            // The §v6 displaced-data contract: state record has no fields,
            // Upkeep.OnEnter reads ActivePlayer from the Match singleton at
            // generate time. Call OnEnter directly and inspect the command.
            var world = new World();
            world.Match.ActivePlayer = PlayerId.Player2;

            var generated = new UpkeepState().OnEnter(new ProbeScope(world, new UpkeepState()));
            var draw = generated.OfType<DrawCardCommand>().Single();
            Assert.AreEqual(PlayerId.Player2, draw.Player);
        }

        private sealed class ProbeScope : IStateScope
        {
            public World     World   { get; }
            public GameState Current { get; }
            public ProbeScope(World w, GameState c) { World = w; Current = c; }
        }

        [Test]
        public void EndState_OnExit_NotUsed_OnEnterCarriesAllCleanup()
        {
            // EndState's cleanup work lives entirely in OnEnter, not OnExit.
            // OnExit is on the *previous* state (Main2 in this case) and stays
            // empty. This test pins that contract.
            Assert.AreEqual(0, new Main2State().OnExit(NullScope()).Count);
            Assert.IsTrue   (new EndState ().OnEnter(NullScope()).Count > 0);
        }

        [Test]
        public void GeneratorOutputs_AreOrderedSameAsReturnedList()
        {
            // EndState returns commands in a deliberate order — clears first,
            // then IncrementTurn, then SwapActivePlayer. The push handler must
            // preserve that order when enqueueing.
            var (engine, state) = MtgBootstrap.Build();
            var atk = MtgBootstrap.SpawnCreature(engine.World, PlayerId.Player1, power: 1, toughness: 1);

            engine.Enqueue(new PassCommand());
            engine.Enqueue(new PassCommand());
            engine.Enqueue(new PassCommand());
            engine.Enqueue(new ToggleAttackerCommand(atk.Id));
            engine.Enqueue(new CommitAttackersCommand());
            engine.Enqueue(new CommitBlockersCommand());
            engine.Enqueue(new PassCommand());   // → End → drains generators → Upkeep

            // Find the EndState push and verify the generator events come in order.
            var log = engine.World.Log;
            int endPushIdx = -1;
            for (int i = 0; i < log.Count; i++)
            {
                if (log[i] is StatePushEvent sp && sp.NewKind == 16) { endPushIdx = i; break; }
            }
            Assert.GreaterOrEqual(endPushIdx, 0);

            // Scan forward from End push for the expected order.
            string[] expected = { "ClearTagCommand", "ClearTagCommand", "ClearTagCommand",
                                  "IncrementTurnCommand", "SwapActivePlayerCommand" };
            int next = 0;
            for (int i = endPushIdx + 1; i < log.Count && next < expected.Length; i++)
            {
                if (log[i] is CommandExecutedEvent ce && ce.CommandName == expected[next])
                    next++;
            }
            Assert.AreEqual(expected.Length, next, "Generator outputs landed out of order.");
        }

        private static IStateScope NullScope() => new EmptyScope();

        private sealed class EmptyScope : IStateScope
        {
            public World     World   { get; } = new World();
            public GameState Current { get; } = new MatchNotStartedState();
        }
    }
}
