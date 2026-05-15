#nullable enable
using System.Linq;
using NUnit.Framework;
using VerticalSlice.Engine.Pipeline;
using VerticalSlice.Engine.Stream;
using VerticalSlice.Sample;

namespace VerticalSlice.Tests
{
    public class ArmorTests
    {
        [Test]
        public void Armor_ReducesDamage()
        {
            var eng = new GameEngine();
            ArmorRule.Register(eng.EffectStore);

            var caster = eng.World.Create();
            var target = eng.World.Create();
            target.SetHealth(30);
            target.SetArmor(2);

            eng.Stack.Push(new StackFrame(new Fireball(caster, target)));   // 6 − 2 = 4

            Assert.That(target.Health(), Is.EqualTo(26));
            Assert.That(target.Armor(), Is.EqualTo(2));   // permanent
        }

        [Test]
        public void Armor_FloorsAtZero_NoHpChange()
        {
            var eng = new GameEngine();
            ArmorRule.Register(eng.EffectStore);

            var caster = eng.World.Create();
            var target = eng.World.Create();
            target.SetHealth(30);
            target.SetArmor(99);

            int checkpoint = eng.World.Log.Count;
            eng.Stack.Push(new StackFrame(new Fireball(caster, target)));

            Assert.That(target.Health(), Is.EqualTo(30));
            // Amount floored to 0 → DealDamage.Apply early-returns → no Health event.
            var added = eng.World.Log.Events.Skip(checkpoint).ToList();
            Assert.That(added.OfType<TagChangeEvent>().Any(e => e.Key == "health"), Is.False);
            Assert.That(added.OfType<TagSetEvent>().Any(e => e.Key == "health"), Is.False);
        }

        [Test]
        public void ZeroArmor_PassesThrough()
        {
            var eng = new GameEngine();
            ArmorRule.Register(eng.EffectStore);

            var caster = eng.World.Create();
            var target = eng.World.Create();
            target.SetHealth(30);

            eng.Stack.Push(new StackFrame(new Fireball(caster, target)));

            Assert.That(target.Health(), Is.EqualTo(24));
        }

        [Test]
        public void ShieldRunsBeforeArmor_ShieldEatsHit_ArmorUntouched()
        {
            var eng = new GameEngine();
            DivineShieldRule.Register(eng.EffectStore);
            ArmorRule.Register(eng.EffectStore);

            var caster = eng.World.Create();
            var target = eng.World.Create();
            target.SetHealth(30);
            target.SetArmor(2);
            target.SetDivineShield(true);

            eng.Stack.Push(new StackFrame(new Fireball(caster, target)));

            Assert.That(target.Health(), Is.EqualTo(30));
            Assert.That(target.DivineShield(), Is.False);
            Assert.That(target.Armor(), Is.EqualTo(2));
        }

        [Test]
        public void Armor_StacksWithSecondShot_AfterShieldBroken()
        {
            var eng = new GameEngine();
            DivineShieldRule.Register(eng.EffectStore);
            ArmorRule.Register(eng.EffectStore);

            var caster = eng.World.Create();
            var target = eng.World.Create();
            target.SetHealth(30);
            target.SetArmor(2);
            target.SetDivineShield(true);

            eng.Stack.Push(new StackFrame(new Fireball(caster, target)));   // shield absorbs
            eng.Stack.Push(new StackFrame(new Fireball(caster, target)));   // 6 − 2 = 4 dmg

            Assert.That(target.Health(), Is.EqualTo(26));
        }
    }
}
