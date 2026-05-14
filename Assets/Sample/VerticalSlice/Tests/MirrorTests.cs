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
            var eng = new GameEngine();
            var caster = eng.World.Create();
            var target = eng.World.Create();
            target.SetHealth(30);

            eng.Effects.Run(new Fireball(caster, target));

            var mirror = new MirrorWorld();
            var scrub = new ScrubController(mirror, eng.World.Log.Events);
            scrub.GoTo(eng.World.Log.Count);

            Assert.That(mirror.Get(target.Id)!.Tags["health"], Is.EqualTo(24));
        }

        [Test]
        public void Mirror_ReverseApply_RestoresPriorValue()
        {
            var eng = new GameEngine();
            var target = eng.World.Create();
            target.SetHealth(30);
            eng.Commands.Execute(new DealDamage(null, target, 6));   // 30 → 24
            eng.Commands.Execute(new DealDamage(null, target, 4));   // 24 → 20

            var mirror = new MirrorWorld();
            var scrub = new ScrubController(mirror, eng.World.Log.Events);
            scrub.GoTo(eng.World.Log.Count);
            Assert.That(mirror.Get(target.Id)!.Tags["health"], Is.EqualTo(20));

            // Step back one TagChange (the 24 → 20 inside the second DealDamage block).
            // We need to scrub back past that change but before the second BlockEnd.
            // Easier: scrub to a point before the second DealDamage opens.
            // Locate the second BlockStart's index:
            var log = eng.World.Log.Events;
            int secondDealStart = -1, count = 0;
            for (int i = 0; i < log.Count; i++)
            {
                if (log[i] is VerticalSlice.Engine.Stream.BlockStartEvent bs
                    && bs.Name == "DealDamage")
                {
                    if (++count == 2) { secondDealStart = i; break; }
                }
            }
            Assert.That(secondDealStart, Is.GreaterThan(0));

            scrub.GoTo(secondDealStart);
            Assert.That(mirror.Get(target.Id)!.Tags["health"], Is.EqualTo(24));
        }

        [Test]
        public void Mirror_ReverseToZero_RemovesEntity()
        {
            var eng = new GameEngine();
            var target = eng.World.Create();
            target.SetHealth(30);
            eng.Commands.Execute(new DealDamage(null, target, 6));

            var mirror = new MirrorWorld();
            var scrub = new ScrubController(mirror, eng.World.Log.Events);
            scrub.GoTo(eng.World.Log.Count);
            Assert.That(mirror.Contains(target.Id), Is.True);

            scrub.GoTo(0);
            Assert.That(mirror.Contains(target.Id), Is.False);
        }

        [Test]
        public void Mirror_RoundTrip_ForwardBackForward_MatchesEachTime()
        {
            var eng = new GameEngine();
            var target = eng.World.Create();
            target.SetHealth(30);
            eng.Commands.Execute(new DealDamage(null, target, 6));   // 24

            int head = eng.World.Log.Count;
            var mirror = new MirrorWorld();
            var scrub = new ScrubController(mirror, eng.World.Log.Events);

            scrub.GoTo(head);
            Assert.That(mirror.Get(target.Id)!.Tags["health"], Is.EqualTo(24));

            scrub.GoTo(0);
            Assert.That(mirror.Contains(target.Id), Is.False);

            scrub.GoTo(head);
            Assert.That(mirror.Get(target.Id)!.Tags["health"], Is.EqualTo(24));
        }
    }
}
