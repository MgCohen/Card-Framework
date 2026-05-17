#nullable enable
using System.Linq;
using NUnit.Framework;
using StateMachine.Engine;
using StateMachine.Sample;

namespace StateMachine.Tests
{
    /// <summary>
    /// Verticals from spike §v6 — the push lifecycle:
    ///   1. old.OnExit → enqueued
    ///   2. emit StatePushEvent
    ///   3. swap Current
    ///   4. new.OnEnter → enqueued
    /// </summary>
    public class PushLifecycleTests
    {
        [Test]
        public void InitialState_IsMatchNotStarted()
        {
            var (_, state) = MtgBootstrap.Build();
            Assert.IsInstanceOf<MatchNotStartedState>(state.Current);
        }

        [Test]
        public void Pass_FromMatchNotStarted_AdvancesIntoUpkeep()
        {
            var (engine, state) = MtgBootstrap.Build();
            engine.Enqueue(new PassCommand());
            Assert.IsInstanceOf<UpkeepState>(state.Current);
        }

        [Test]
        public void Push_EmitsStatePushEvent_WithCorrectKinds()
        {
            var (engine, _) = MtgBootstrap.Build();
            engine.Enqueue(new PassCommand());

            var pushes = engine.World.Log.OfType<StatePushEvent>().ToList();
            Assert.AreEqual(1, pushes.Count);
            Assert.AreEqual(0,  pushes[0].PrevKind);   // MatchNotStarted
            Assert.AreEqual(10, pushes[0].NewKind);    // Upkeep
        }

        [Test]
        public void OnEnter_GeneratorOutputs_LandAsEvents_AfterStatePush()
        {
            var (engine, _) = MtgBootstrap.Build();
            engine.Enqueue(new PassCommand());

            // The order in the log must be: pass executed, state push, untap, draw.
            // OnEnter outputs are enqueued at the tail by the push handler, so
            // they fire AFTER the StatePushEvent.
            var names = engine.World.Log
                .Select(ev => ev switch
                {
                    StatePushEvent sp       => $"PUSH:{sp.PrevKind}->{sp.NewKind}",
                    CommandExecutedEvent ce => ce.CommandName,
                    _                       => ev.GetType().Name
                })
                .ToList();

            Assert.Contains("PassCommand",      names);
            Assert.Contains("PUSH:0->10",       names);
            Assert.Contains("UntapAllCommand",  names);
            Assert.Contains("DrawCardCommand",  names);

            // Critical ordering: the push happens before the OnEnter outputs.
            int pushIdx  = names.IndexOf("PUSH:0->10");
            int untapIdx = names.IndexOf("UntapAllCommand");
            int drawIdx  = names.IndexOf("DrawCardCommand");
            Assert.Less(pushIdx, untapIdx);
            Assert.Less(pushIdx, drawIdx);
        }

        [Test]
        public void Pass_DoesNotTrigger_WhenIsCompletedReturnsFalse()
        {
            var (engine, state) = MtgBootstrap.Build();
            engine.Enqueue(new PassCommand());   // → Upkeep
            engine.Enqueue(new PassCommand());   // → Main1 (Upkeep completes on PassCommand)

            // We're in Main1 now. ToggleAttackerCommand isn't an action Main1
            // listens for, but more importantly Main1.IsCompleted only triggers
            // on Pass — sending some other command must NOT push.
            Assert.IsInstanceOf<Main1State>(state.Current);
            engine.Enqueue(new ToggleAttackerCommand(CardId: 999));
            Assert.IsInstanceOf<Main1State>(state.Current);
        }
    }
}
