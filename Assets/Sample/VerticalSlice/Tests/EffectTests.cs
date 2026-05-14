#nullable enable
using System.Linq;
using NUnit.Framework;
using VerticalSlice.Engine.Pipeline;
using VerticalSlice.Engine.Stream;
using VerticalSlice.Sample;

namespace VerticalSlice.Tests
{
    public class EffectTests
    {
        [Test]
        public void RunEffect_OpensBlockNamedAfterEffect()
        {
            var eng = new GameEngine();
            var caster = eng.World.Create();
            var target = eng.World.Create(); target.SetHp(30);
            int checkpoint = eng.World.Log.Count;

            eng.Effects.Run(new Fireball(caster, target));

            var blockStarts = eng.World.Log.Events.Skip(checkpoint)
                                          .OfType<BlockStartEvent>().ToList();
            Assert.That(blockStarts.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(blockStarts[0].Name, Is.EqualTo("Fireball"));
            Assert.That(blockStarts[1].Name, Is.EqualTo("DealDamage"));
        }

        [Test]
        public void RunEffect_PayloadCapturesPublicProperties()
        {
            var eng = new GameEngine();
            var caster = eng.World.Create();
            var target = eng.World.Create();
            int checkpoint = eng.World.Log.Count;

            eng.Effects.Run(new Fireball(caster, target));

            var fireballStart = eng.World.Log.Events.Skip(checkpoint)
                                               .OfType<BlockStartEvent>()
                                               .First(b => b.Name == "Fireball");
            Assert.That(fireballStart.Payload.ContainsKey("Caster"), Is.True);
            Assert.That(fireballStart.Payload.ContainsKey("Target"), Is.True);
            Assert.That(fireballStart.Payload["Caster"], Is.SameAs(caster));
            Assert.That(fireballStart.Payload["Target"], Is.SameAs(target));
        }

        [Test]
        public void Execute_OpensBlock_AndAppliesMutation()
        {
            var eng = new GameEngine();
            var target = eng.World.Create(); target.SetHp(30);
            int checkpoint = eng.World.Log.Count;

            eng.Commands.Execute(new DealDamage(null, target, 6));

            Assert.That(target.Hp(), Is.EqualTo(24));
            var added = eng.World.Log.Events.Skip(checkpoint).ToList();
            Assert.That(added[0], Is.TypeOf<BlockStartEvent>());
            Assert.That(((BlockStartEvent)added[0]).Name, Is.EqualTo("DealDamage"));
            Assert.That(added.OfType<TagChangeEvent>().Count(), Is.EqualTo(1));
            Assert.That(added.Last(), Is.TypeOf<BlockEndEvent>());
        }

        [Test]
        public void HookFiresInBeforeAndAfterPhases()
        {
            var eng = new GameEngine();
            var target = eng.World.Create(); target.SetHp(30);

            bool beforeFired = false, afterFired = false;
            eng.Commands.Subscribe<DealDamage>(HookPhase.Before, HookPriority.OnEffect,
                (cmd, scope) => beforeFired = true);
            eng.Commands.Subscribe<DealDamage>(HookPhase.After, HookPriority.Reaction,
                (cmd, scope) => afterFired = true);

            eng.Commands.Execute(new DealDamage(null, target, 6));

            Assert.That(beforeFired, Is.True);
            Assert.That(afterFired, Is.True);
            Assert.That(target.Hp(), Is.EqualTo(24));
        }

        [Test]
        public void Hook_BeforeReplacement_RunsBeforeApply()
        {
            var eng = new GameEngine();
            var target = eng.World.Create(); target.SetHp(30);

            // Replacement-band hook zeroes Amount before Apply runs.
            eng.Commands.Subscribe<DealDamage>(HookPhase.Before, HookPriority.Replacement,
                (cmd, scope) => cmd.Amount = 0);

            eng.Commands.Execute(new DealDamage(null, target, 6));

            Assert.That(target.Hp(), Is.EqualTo(30),
                "Replacement-band Before hook must mutate Amount before Apply.");
        }
    }
}
