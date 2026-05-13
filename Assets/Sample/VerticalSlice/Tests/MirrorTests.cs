#nullable enable
using NUnit.Framework;
using VerticalSlice.Engine.Pipeline;
using VerticalSlice.Mirror;
using VerticalSlice.Sample;

namespace VerticalSlice.Tests
{
    public class MirrorTests
    {
        [Test]
        public void Mirror_ReplaysToHead_MatchesPrimary()
        {
            var w = new World();
            var c = w.Create<Card>();
            c.Hp = 30;
            w.Execute(new DealDamage(null, c, 6));
            w.Execute(new DealDamage(null, c, 4));

            var mirror = new MirrorWorld();
            var scrub = new ScrubController(mirror, w.EventLog.Events);
            scrub.GoTo(w.EventLog.Count);

            var ent = mirror.Get(c.Id);
            Assert.That(ent, Is.Not.Null);
            Assert.That(ent!.Tags["hp"], Is.EqualTo(20));
        }

        [Test]
        public void Mirror_ReverseApply_RestoresPriorValue()
        {
            var w = new World();
            var c = w.Create<Card>();
            c.Hp = 30;
            w.Execute(new DealDamage(null, c, 6));     // hp: 30 -> 24
            int checkpoint = w.EventLog.Count;
            w.Execute(new DealDamage(null, c, 4));     // hp: 24 -> 20

            var mirror = new MirrorWorld();
            var scrub = new ScrubController(mirror, w.EventLog.Events);
            scrub.GoTo(w.EventLog.Count);
            Assert.That(mirror.Get(c.Id)!.Tags["hp"], Is.EqualTo(20));

            scrub.GoTo(checkpoint);
            Assert.That(mirror.Get(c.Id)!.Tags["hp"], Is.EqualTo(24));
        }

        [Test]
        public void Mirror_ReverseToZero_RemovesEntity()
        {
            var w = new World();
            var c = w.Create<Card>();
            c.Hp = 30;

            var mirror = new MirrorWorld();
            var scrub = new ScrubController(mirror, w.EventLog.Events);
            scrub.GoTo(w.EventLog.Count);
            Assert.That(mirror.Contains(c.Id), Is.True);

            scrub.GoTo(0);
            Assert.That(mirror.Contains(c.Id), Is.False);
        }

        [Test]
        public void Mirror_RoundTrip_ForwardThenBackThenForward_MatchesEachTime()
        {
            var w = new World();
            var c = w.Create<Card>();
            c.Hp = 30;
            w.Execute(new DealDamage(null, c, 6));
            w.Execute(new DealDamage(null, c, 4));

            var mirror = new MirrorWorld();
            var scrub = new ScrubController(mirror, w.EventLog.Events);

            scrub.GoTo(w.EventLog.Count);
            Assert.That(mirror.Get(c.Id)!.Tags["hp"], Is.EqualTo(20));

            scrub.GoTo(0);
            Assert.That(mirror.Contains(c.Id), Is.False);

            scrub.GoTo(w.EventLog.Count);
            Assert.That(mirror.Get(c.Id)!.Tags["hp"], Is.EqualTo(20));
        }
    }
}
