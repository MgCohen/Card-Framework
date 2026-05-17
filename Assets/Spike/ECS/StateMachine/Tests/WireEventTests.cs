#nullable enable
using System.Linq;
using NUnit.Framework;
using StateMachine.Engine;
using StateMachine.Sample;

namespace StateMachine.Tests
{
    /// <summary>
    /// Spike §v6 wire format: every transition is a single 2-byte event.
    /// No polymorphism — kind bytes resolve back to record instances through
    /// the registry. Replay walks the log; symmetric apply/unapply for state
    /// pushes.
    /// </summary>
    public class WireEventTests
    {
        [Test]
        public void StatePushEvent_PayloadIsTwoBytes_NewAndPrev()
        {
            // Smoke: the record only has two byte fields. If someone added
            // anything else to the wire payload, this would break.
            var ev = new StatePushEvent(newKind: 14, prevKind: 13);
            Assert.AreEqual(14, ev.NewKind);
            Assert.AreEqual(13, ev.PrevKind);

            var fields = typeof(StatePushEvent).GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.DeclaredOnly);
            Assert.LessOrEqual(fields.Length, 2,
                "StatePushEvent must stay narrow — the §v6 contract is 2 bytes.");
        }

        [Test]
        public void Registry_RoundTrips_KindToRecord()
        {
            var r = MtgBootstrap.BuildStateRegistry();

            // Round-trip every registered kind.
            foreach (var (kind, type) in r.All)
            {
                var instance = r.Resolve(kind);
                Assert.AreEqual(type, instance.GetType());
                Assert.AreEqual(kind, r.KindOf(instance));
            }
        }

        [Test]
        public void Replay_ReproducesCursorByApplyingPushEventsOnly()
        {
            // Run a session, capture the StatePushEvents, then replay them
            // on a fresh registry-backed cursor. Final cursor must match.
            var (engine, state) = MtgBootstrap.Build();
            engine.Enqueue(new PassCommand());     // MatchNotStarted → Upkeep
            engine.Enqueue(new PassCommand());     // Upkeep → Main1
            engine.Enqueue(new PassCommand());     // Main1 → DeclareAttackers

            var pushes = engine.World.Log.OfType<StatePushEvent>().ToList();
            Assert.AreEqual(3, pushes.Count);

            // Replay using only the kind bytes — no record state needed.
            var registry = MtgBootstrap.BuildStateRegistry();
            GameState cursor = registry.Resolve(pushes[0].PrevKind);
            foreach (var p in pushes)
            {
                Assert.AreEqual(registry.KindOf(cursor), p.PrevKind);
                cursor = registry.Resolve(p.NewKind);
            }

            Assert.AreEqual(state.Current.GetType(), cursor.GetType());
        }

        [Test]
        public void Replay_Unapply_RestoresPreviousCursor()
        {
            // Symmetric do/undo — apply NewKind to advance, PrevKind to undo.
            var registry = MtgBootstrap.BuildStateRegistry();
            var push     = new StatePushEvent(newKind: 12, prevKind: 11);

            GameState cursor = registry.Resolve(push.PrevKind);
            Assert.IsInstanceOf<Main1State>(cursor);

            // Apply.
            cursor = registry.Resolve(push.NewKind);
            Assert.IsInstanceOf<DeclareAttackersState>(cursor);

            // Unapply.
            cursor = registry.Resolve(push.PrevKind);
            Assert.IsInstanceOf<Main1State>(cursor);
        }
    }
}
