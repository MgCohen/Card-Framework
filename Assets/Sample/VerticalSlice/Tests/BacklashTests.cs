#nullable enable
using System.Linq;
using NUnit.Framework;
using VerticalSlice.Engine.Pipeline;
using VerticalSlice.Engine.Stream;
using VerticalSlice.Sample;

namespace VerticalSlice.Tests
{
    public class BacklashTests
    {
        [Test]
        public void Backlash_DealingDamage_RetaliatesOneBack()
        {
            var eng = new GameEngine();
            BacklashRule.Register(eng.Effects);

            var caster = eng.World.Create();
            var target = eng.World.Create();
            caster.SetHealth(30); caster.SetMaxHealth(30);
            target.SetHealth(30); target.SetMaxHealth(30);
            caster.SetBacklash(true);

            eng.Effects.Run(new Fireball(caster, target));

            Assert.That(target.Health(), Is.EqualTo(24));   // 6 from Fireball
            Assert.That(caster.Health(), Is.EqualTo(29));   // 1 backlash
        }

        [Test]
        public void Backlash_OpensNestedBlock_NamedBacklash()
        {
            var eng = new GameEngine();
            BacklashRule.Register(eng.Effects);

            var caster = eng.World.Create();
            var target = eng.World.Create();
            caster.SetHealth(30); caster.SetMaxHealth(30);
            target.SetHealth(30); target.SetMaxHealth(30);
            caster.SetBacklash(true);

            int checkpoint = eng.World.Log.Count;
            eng.Effects.Run(new Fireball(caster, target));

            var blockNames = eng.World.Log.Events.Skip(checkpoint)
                                .OfType<BlockStartEvent>()
                                .Select(b => b.Name)
                                .ToList();

            // Expected nesting:
            //   Fireball → DealDamage → BacklashRule → Backlash → DealDamage
            // The BacklashRule block is the rule itself firing (Effect<DealDamage>);
            // inside it the Backlash effect fires; inside that the retaliation
            // DealDamage. Honest causal chain — each level says why the next
            // happened.
            Assert.That(blockNames, Is.EqualTo(new[]
            {
                "Fireball", "DealDamage", "BacklashRule", "Backlash", "DealDamage",
            }));
        }

        [Test]
        public void Backlash_OnlyFiresWhenSourceHasTag()
        {
            var eng = new GameEngine();
            BacklashRule.Register(eng.Effects);

            var caster = eng.World.Create();
            var target = eng.World.Create();
            caster.SetHealth(30); caster.SetMaxHealth(30);
            target.SetHealth(30); target.SetMaxHealth(30);
            // caster.Backlash stays false.

            int checkpoint = eng.World.Log.Count;
            eng.Effects.Run(new Fireball(caster, target));

            Assert.That(caster.Health(), Is.EqualTo(30));   // untouched
            var hasBacklashBlock = eng.World.Log.Events.Skip(checkpoint)
                .OfType<BlockStartEvent>().Any(b => b.Name == "Backlash");
            Assert.That(hasBacklashBlock, Is.False);
        }

        [Test]
        public void Backlash_DoesNotRecurse_TargetWithoutTagDoesNotChain()
        {
            var eng = new GameEngine();
            BacklashRule.Register(eng.Effects);

            var caster = eng.World.Create();
            var target = eng.World.Create();
            caster.SetHealth(30); caster.SetMaxHealth(30);
            target.SetHealth(30); target.SetMaxHealth(30);
            caster.SetBacklash(true);
            // target.Backlash stays false → retaliation's source has no Backlash tag.

            int checkpoint = eng.World.Log.Count;
            eng.Effects.Run(new Fireball(caster, target));

            var triggers = eng.World.Log.Events.Skip(checkpoint)
                .OfType<BlockStartEvent>().Where(b => b.Name == "Backlash").ToList();
            Assert.That(triggers.Count, Is.EqualTo(1));
        }

        [Test]
        public void Backlash_DoesNotFire_WhenDamageWasZeroed()
        {
            var eng = new GameEngine();
            DivineShieldRule.Register(eng.Effects);
            BacklashRule.Register(eng.Effects);

            var caster = eng.World.Create();
            var target = eng.World.Create();
            caster.SetHealth(30); caster.SetMaxHealth(30);
            target.SetHealth(30); target.SetMaxHealth(30);
            caster.SetBacklash(true);
            target.SetDivineShield(true);

            eng.Effects.Run(new Fireball(caster, target));

            Assert.That(target.Health(), Is.EqualTo(30));
            Assert.That(target.DivineShield(), Is.False);
            Assert.That(caster.Health(), Is.EqualTo(30),
                "Backlash must skip when the original damage was zeroed.");
        }
    }
}
