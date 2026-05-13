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
            var w = new World();
            DivineShieldRule.Register(w.Hooks);

            var caster = w.Create<Card>();
            var target = w.Create<Card>();
            target.Hp = 30;
            target.DivineShield = true;

            int checkpoint = w.EventLog.Count;
            Fireball.Cast(w, caster, target);

            Assert.That(target.DivineShield, Is.False);
            Assert.That(target.Hp, Is.EqualTo(30));

            var added = w.EventLog.Events.Skip(checkpoint).ToList();
            var tagChanges = added.OfType<TagChangeEvent>().ToList();
            Assert.That(tagChanges.Count, Is.EqualTo(1));
            Assert.That(tagChanges[0].Key, Is.EqualTo("divineShield"));
            Assert.That(tagChanges[0].Next, Is.EqualTo(false));
        }

        [Test]
        public void NoShield_DamageApplies()
        {
            var w = new World();
            DivineShieldRule.Register(w.Hooks);

            var caster = w.Create<Card>();
            var target = w.Create<Card>();
            target.Hp = 30;

            Fireball.Cast(w, caster, target);

            Assert.That(target.Hp, Is.EqualTo(24));
        }

        [Test]
        public void SecondShot_AfterShieldBroke_AppliesDamage()
        {
            var w = new World();
            DivineShieldRule.Register(w.Hooks);

            var caster = w.Create<Card>();
            var target = w.Create<Card>();
            target.Hp = 30;
            target.DivineShield = true;

            Fireball.Cast(w, caster, target);  // shield absorbs
            Fireball.Cast(w, caster, target);  // damage lands

            Assert.That(target.DivineShield, Is.False);
            Assert.That(target.Hp, Is.EqualTo(24));
        }
    }
}
