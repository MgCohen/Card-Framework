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
            var target = eng.EntityStore.Create();
            target.SetMaxHealth(30);
            target.SetHealth(20);

            eng.Stack.Push(new StackFrame(new HealingTouch(null, target)));

            Assert.That(target.Health(), Is.EqualTo(28));   // 20 + 8
        }

        [Test]
        public void Heal_ClampsAtMax()
        {
            var eng = new GameEngine();
            var target = eng.EntityStore.Create();
            target.SetMaxHealth(30);
            target.SetHealth(28);

            eng.Stack.Push(new StackFrame(new HealingTouch(null, target)));

            Assert.That(target.Health(), Is.EqualTo(30));   // 28+8 capped at 30
        }

        [Test]
        public void Heal_AtFullHealth_EmitsNoHealthTagEvent()
        {
            var eng = new GameEngine();
            var target = eng.EntityStore.Create();
            target.SetMaxHealth(30);
            target.SetHealth(30);

            int checkpoint = eng.World.Log.Count;
            eng.Stack.Push(new StackFrame(new HealingTouch(null, target)));

            var added = eng.World.Log.Events.Skip(checkpoint).ToList();
            // Two blocks open (HealingTouch + Heal) even when Apply is a no-op.
            Assert.That(added.OfType<BlockStartEvent>().Count(), Is.EqualTo(2));
            Assert.That(added.OfType<BlockEndEvent>().Count(), Is.EqualTo(2));
            // But no health-change event.
            Assert.That(added.OfType<TagChangeEvent>().Any(e => e.Key == "health"), Is.False);
            Assert.That(added.OfType<TagSetEvent>().Any(e => e.Key == "health"), Is.False);
        }

        [Test]
        public void Heal_IgnoresDivineShield_AndArmor()
        {
            // Shield + Armor hooks attach to DealDamage. Heal is a different
            // command type → bus dispatches by exact type → hooks don't fire.
            var eng = new GameEngine();
            DivineShieldRule.Register(eng.EffectStore);
            ArmorRule.Register(eng.EffectStore);

            var target = eng.EntityStore.Create();
            target.SetMaxHealth(30);
            target.SetHealth(20);
            target.SetDivineShield(true);
            target.SetArmor(99);

            eng.Stack.Push(new StackFrame(new HealingTouch(null, target)));

            Assert.That(target.Health(), Is.EqualTo(28));   // 20 + 8
            Assert.That(target.DivineShield(), Is.True);
            Assert.That(target.Armor(), Is.EqualTo(99));
        }
    }
}
