#nullable enable
using System.Linq;
using NUnit.Framework;
using VerticalSlice.Engine.Pipeline;
using VerticalSlice.Engine.Stream;
using VerticalSlice.Sample;

namespace VerticalSlice.Tests
{
    public class HealTests
    {
        [Test]
        public void Heal_BelowMax_AddsAmount()
        {
            var eng = new GameEngine();
            var target = eng.World.Create();
            target.SetMaxHp(30);
            target.SetHp(20);

            eng.Effects.Run(new HealingPotion(null, target));

            Assert.That(target.Hp(), Is.EqualTo(24));
        }

        [Test]
        public void Heal_ClampsAtMax()
        {
            var eng = new GameEngine();
            var target = eng.World.Create();
            target.SetMaxHp(30);
            target.SetHp(28);

            eng.Effects.Run(new HealingPotion(null, target));

            Assert.That(target.Hp(), Is.EqualTo(30));   // 28+4 capped at 30
        }

        [Test]
        public void Heal_AtFullHp_EmitsNoHpTagEvent()
        {
            var eng = new GameEngine();
            var target = eng.World.Create();
            target.SetMaxHp(30);
            target.SetHp(30);

            int checkpoint = eng.World.Log.Count;
            eng.Effects.Run(new HealingPotion(null, target));

            var added = eng.World.Log.Events.Skip(checkpoint).ToList();
            // Two blocks open (HealingPotion + Heal) even when Apply is a no-op.
            Assert.That(added.OfType<BlockStartEvent>().Count(), Is.EqualTo(2));
            Assert.That(added.OfType<BlockEndEvent>().Count(), Is.EqualTo(2));
            // But no Hp-change event.
            Assert.That(added.OfType<TagChangeEvent>().Any(e => e.Key == "hp"), Is.False);
            Assert.That(added.OfType<TagSetEvent>().Any(e => e.Key == "hp"), Is.False);
        }

        [Test]
        public void Heal_IgnoresDivineShield_AndArmor()
        {
            // Shield + Armor hooks attach to DealDamage. Heal is a different
            // command type → bus dispatches by exact type → hooks don't fire.
            var eng = new GameEngine();
            DivineShieldRule.Register(eng.Effects);
            ArmorRule.Register(eng.Effects);

            var target = eng.World.Create();
            target.SetMaxHp(30);
            target.SetHp(20);
            target.SetDivineShield(true);
            target.SetArmor(99);

            eng.Effects.Run(new HealingPotion(null, target));

            Assert.That(target.Hp(), Is.EqualTo(24));
            Assert.That(target.DivineShield(), Is.True);
            Assert.That(target.Armor(), Is.EqualTo(99));
        }
    }
}
