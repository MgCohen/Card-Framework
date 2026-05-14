#nullable enable
using NUnit.Framework;
using VerticalSlice.Engine.Pipeline;
using VerticalSlice.Mirror;
using VerticalSlice.Sample;

namespace VerticalSlice.Tests
{
    /// <summary>
    /// Targeted tests for the "rewinding past a tag's first write leaves the
    /// mirror stuck at default" bug. Fixed by splitting wire events into
    /// <c>TagSetEvent</c> (first write — Unapply removes the key) and
    /// <c>TagChangeEvent</c> (subsequent write — Unapply writes Prev).
    /// </summary>
    public class MirrorInitValueTests
    {
        [Test]
        public void Mirror_RewindPastFirstWrite_RemovesKeyEntirely()
        {
            var eng = new GameEngine();
            var e = eng.World.Create();
            e.SetHp(30);   // first write — emits TagSet

            var mirror = new MirrorWorld();
            var scrub = new ScrubController(mirror, eng.World.Log.Events);
            scrub.GoTo(eng.World.Log.Count);

            Assert.That(mirror.Get(e.Id)!.Tags.ContainsKey("hp"), Is.True);
            Assert.That(mirror.Get(e.Id)!.Tags["hp"], Is.EqualTo(30));

            // Rewind past the TagSet — the key must be REMOVED, not set to 0.
            scrub.GoTo(1);   // only the EntityCreated applied
            Assert.That(mirror.Get(e.Id)!.Tags.ContainsKey("hp"), Is.False,
                "After unapplying TagSet, the key must be absent — distinguishable " +
                "from 'set to default(int) = 0'.");
        }

        [Test]
        public void Mirror_RewindPastFirstWrite_DistinguishableFromDefault()
        {
            // The whole point: 'absent' vs 'set to default(T)' must be observably
            // different. With nullable references this would matter; with int it
            // matters because some code reads .Has(key) to check existence.
            var eng = new GameEngine();
            var e = eng.World.Create();
            e.SetHp(0);    // explicitly set to default(int) — emits TagSet

            var mirror = new MirrorWorld();
            var scrub = new ScrubController(mirror, eng.World.Log.Events);
            scrub.GoTo(eng.World.Log.Count);

            Assert.That(mirror.Get(e.Id)!.Tags.ContainsKey("hp"), Is.True,
                "After applying TagSet(value=0), key must be present.");

            scrub.GoTo(1);   // unapply the TagSet
            Assert.That(mirror.Get(e.Id)!.Tags.ContainsKey("hp"), Is.False,
                "After unapplying TagSet, key must be absent regardless of value=0.");
        }

        [Test]
        public void Mirror_RewindThroughChainOfWrites_RestoresIntermediateValues()
        {
            // First write 30 (TagSet), then 25 (TagChange), then 10 (TagChange).
            // Rewind one step at a time; check intermediate values restore.
            var eng = new GameEngine();
            var e = eng.World.Create();
            e.SetHp(30);
            e.SetHp(25);
            e.SetHp(10);

            int head = eng.World.Log.Count;
            var mirror = new MirrorWorld();
            var scrub = new ScrubController(mirror, eng.World.Log.Events);

            scrub.GoTo(head);
            Assert.That(mirror.Get(e.Id)!.Tags["hp"], Is.EqualTo(10));

            scrub.GoTo(head - 1);   // unapply the 25→10 TagChange
            Assert.That(mirror.Get(e.Id)!.Tags["hp"], Is.EqualTo(25));

            scrub.GoTo(head - 2);   // unapply the 30→25 TagChange
            Assert.That(mirror.Get(e.Id)!.Tags["hp"], Is.EqualTo(30));

            scrub.GoTo(head - 3);   // unapply the initial TagSet — key removed
            Assert.That(mirror.Get(e.Id)!.Tags.ContainsKey("hp"), Is.False);
        }
    }
}
