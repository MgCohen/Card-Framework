#nullable enable
using System;
using System.Linq;
using NUnit.Framework;
using VerticalSlice.Engine.Commands;
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Pipeline;
using VerticalSlice.Engine.Stream;
using VerticalSlice.Sample;

namespace VerticalSlice.Tests
{
    /// <summary>
    /// Tests the metadata fields the engine writes onto BlockStart/BlockEnd:
    /// id (monotonic), parentId (causal chain), kind (Effect vs Command),
    /// source (provenance via convention), outcome (Completed/Errored).
    /// </summary>
    public class BlockMetadataTests
    {
        [Test]
        public void BlockIds_AreMonotonic_StartingAt1()
        {
            var eng = new GameEngine();
            var t = eng.World.Create();
            t.SetHealth(30);

            int checkpoint = eng.World.Log.Count;
            eng.Stack.Push(new StackFrame(new Fireball(null, t)));
            eng.Stack.Push(new StackFrame(new Fireball(null, t)));

            var ids = eng.World.Log.Events.Skip(checkpoint)
                        .OfType<BlockStartEvent>()
                        .Select(b => b.Id)
                        .ToList();
            // Two effects + two nested DealDamage blocks = 4 ids,
            // strictly increasing, starting from wherever the counter was
            // (>=1 for sure).
            Assert.That(ids[0], Is.GreaterThanOrEqualTo(1));
            for (int i = 1; i < ids.Count; i++)
                Assert.That(ids[i], Is.GreaterThan(ids[i-1]));
        }

        [Test]
        public void ParentId_NullForTopLevel_PointsAtEnclosingForNested()
        {
            var eng = new GameEngine();
            var t = eng.World.Create();
            t.SetHealth(30);

            int checkpoint = eng.World.Log.Count;
            eng.Stack.Push(new StackFrame(new Fireball(null, t)));

            var starts = eng.World.Log.Events.Skip(checkpoint)
                          .OfType<BlockStartEvent>().ToList();

            Assert.That(starts.Count, Is.EqualTo(2));
            // Top-level Fireball block: no parent.
            Assert.That(starts[0].Name, Is.EqualTo("Fireball"));
            Assert.That(starts[0].ParentId, Is.Null);
            // Inner DealDamage block: parent is the Fireball block's id.
            Assert.That(starts[1].Name, Is.EqualTo("DealDamage"));
            Assert.That(starts[1].ParentId, Is.EqualTo(starts[0].Id));
        }

        [Test]
        public void BlockKind_IsEffectForRunEffect_CommandForExecute()
        {
            var eng = new GameEngine();
            var t = eng.World.Create();
            t.SetHealth(30);

            int checkpoint = eng.World.Log.Count;
            eng.Stack.Push(new StackFrame(new Fireball(null, t)));
            eng.Enqueue(new DealDamage(null, t, 1));

            var starts = eng.World.Log.Events.Skip(checkpoint)
                          .OfType<BlockStartEvent>().ToList();

            // Effect → kind=Effect, nested DealDamage → kind=Command
            Assert.That(starts[0].Name, Is.EqualTo("Fireball"));
            Assert.That(starts[0].BlockKind, Is.EqualTo(BlockKind.Effect));
            Assert.That(starts[1].Name, Is.EqualTo("DealDamage"));
            Assert.That(starts[1].BlockKind, Is.EqualTo(BlockKind.Command));

            // Top-level Execute → kind=Command
            Assert.That(starts[2].Name, Is.EqualTo("DealDamage"));
            Assert.That(starts[2].BlockKind, Is.EqualTo(BlockKind.Command));
        }

        [Test]
        public void Source_ExtractedFromRunnableSourceProperty()
        {
            var eng = new GameEngine();
            var caster = eng.World.Create();
            var t = eng.World.Create();
            t.SetHealth(30);

            int checkpoint = eng.World.Log.Count;
            eng.Stack.Push(new StackFrame(new Fireball(caster, t)));

            var fireballStart = eng.World.Log.Events.Skip(checkpoint)
                                 .OfType<BlockStartEvent>()
                                 .First(b => b.Name == "Fireball");
            Assert.That(fireballStart.Source, Is.Not.Null);
            Assert.That(fireballStart.Source!.Id, Is.EqualTo(caster.Id));

            // The nested DealDamage's Source is also the same caster
            // (DealDamage.Source = caster, propagated by Fireball.Run).
            var dealStart = eng.World.Log.Events.Skip(checkpoint)
                              .OfType<BlockStartEvent>()
                              .First(b => b.Name == "DealDamage");
            Assert.That(dealStart.Source, Is.Not.Null);
            Assert.That(dealStart.Source!.Id, Is.EqualTo(caster.Id));
        }

        [Test]
        public void Source_NullWhenRunnableHasNoSource()
        {
            var eng = new GameEngine();
            var t = eng.World.Create();
            t.SetHealth(30);

            int checkpoint = eng.World.Log.Count;
            // Fireball with null source
            eng.Stack.Push(new StackFrame(new Fireball(null, t)));

            var fireballStart = eng.World.Log.Events.Skip(checkpoint)
                                 .OfType<BlockStartEvent>()
                                 .First(b => b.Name == "Fireball");
            Assert.That(fireballStart.Source, Is.Null);
        }

        [Test]
        public void BlockEnd_IdMatchesBlockStart_LIFO()
        {
            var eng = new GameEngine();
            var t = eng.World.Create();
            t.SetHealth(30);

            int checkpoint = eng.World.Log.Count;
            eng.Stack.Push(new StackFrame(new Fireball(null, t)));

            var added = eng.World.Log.Events.Skip(checkpoint).ToList();
            var starts = added.OfType<BlockStartEvent>().ToList();
            var ends = added.OfType<BlockEndEvent>().ToList();

            // 2 starts, 2 ends. Inner block ends first (LIFO).
            Assert.That(ends.Count, Is.EqualTo(starts.Count));
            Assert.That(ends[0].Id, Is.EqualTo(starts[1].Id),
                "Inner block (DealDamage) closes first.");
            Assert.That(ends[1].Id, Is.EqualTo(starts[0].Id),
                "Outer block (Fireball) closes last.");
        }

        [Test]
        public void BlockOutcome_CompletedOnNormalRun()
        {
            var eng = new GameEngine();
            var t = eng.World.Create();
            t.SetHealth(30);

            int checkpoint = eng.World.Log.Count;
            eng.Stack.Push(new StackFrame(new Fireball(null, t)));

            var ends = eng.World.Log.Events.Skip(checkpoint)
                         .OfType<BlockEndEvent>().ToList();
            Assert.That(ends.All(e => e.Outcome == BlockOutcome.Completed), Is.True);
        }

        [Test]
        public void BlockOutcome_ErroredWhenExceptionEscapes()
        {
            var eng = new GameEngine();

            int checkpoint = eng.World.Log.Count;
            Assert.Throws<InvalidOperationException>(() =>
                eng.Stack.Push(new StackFrame(new ThrowingEffect())));

            var ends = eng.World.Log.Events.Skip(checkpoint)
                         .OfType<BlockEndEvent>().ToList();
            Assert.That(ends.Count, Is.EqualTo(1));
            Assert.That(ends[0].Outcome, Is.EqualTo(BlockOutcome.Errored));
        }

        // --- Helper effect that throws during Run ---------------------------

        private sealed class ThrowingEffect : Effect
        {
            public override void Run(IEffectScope scope)
            {
                throw new InvalidOperationException("boom");
            }
        }
    }
}
