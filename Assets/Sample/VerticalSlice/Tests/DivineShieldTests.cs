#nullable enable
using System.Linq;
using NUnit.Framework;
using VerticalSlice.Engine.Pipeline;
using VerticalSlice.Engine.Stream;
using VerticalSlice.Sample;

namespace VerticalSlice.Tests
{
    public class DivineShieldTests
    {
        [Test]
        public void Shield_Prevents_FullDamage_AndBreaks()
        {
            var eng = new GameEngine();
            DivineShieldRule.Register(eng.Effects);

            var caster = eng.World.Create();
            var target = eng.World.Create();
            target.SetHp(30);
            target.SetDivineShield(true);

            int checkpoint = eng.World.Log.Count;
            eng.Effects.Run(new Fireball(caster, target));

            Assert.That(target.DivineShield(), Is.False);
            Assert.That(target.Hp(), Is.EqualTo(30));

            // The shield-break write should have flowed through ShieldBreak →
            // BreakDivineShield → TagChange.
            var added = eng.World.Log.Events.Skip(checkpoint).ToList();
            var tagChanges = added.OfType<TagChangeEvent>().ToList();
            Assert.That(tagChanges.Count, Is.EqualTo(1));
            Assert.That(tagChanges[0].Key, Is.EqualTo("divineShield"));
            Assert.That(tagChanges[0].Next, Is.EqualTo(false));

            // ShieldBreak block should appear inside the DealDamage block.
            var blockNames = added.OfType<BlockStartEvent>().Select(b => b.Name).ToList();
            Assert.That(blockNames, Contains.Item("ShieldBreak"));
            Assert.That(blockNames, Contains.Item("BreakDivineShield"));
        }

        [Test]
        public void NoShield_DamageApplies()
        {
            var eng = new GameEngine();
            DivineShieldRule.Register(eng.Effects);

            var caster = eng.World.Create();
            var target = eng.World.Create();
            target.SetHp(30);

            eng.Effects.Run(new Fireball(caster, target));

            Assert.That(target.Hp(), Is.EqualTo(24));
        }

        [Test]
        public void SecondShot_AfterShieldBroke_AppliesDamage()
        {
            var eng = new GameEngine();
            DivineShieldRule.Register(eng.Effects);

            var caster = eng.World.Create();
            var target = eng.World.Create();
            target.SetHp(30);
            target.SetDivineShield(true);

            eng.Effects.Run(new Fireball(caster, target));   // shield absorbs
            eng.Effects.Run(new Fireball(caster, target));   // damage lands

            Assert.That(target.DivineShield(), Is.False);
            Assert.That(target.Hp(), Is.EqualTo(24));
        }
    }
}
