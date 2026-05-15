#nullable enable
using System;
using System.Linq;
using NUnit.Framework;
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Pipeline;
using VerticalSlice.Engine.Stream;
using VerticalSlice.Sample;

namespace VerticalSlice.Tests
{
    public class EffectTests
    {
        // Test-only Effect that runs a closure in its body — lets the
        // hook tests assert "this fired" / mutate the cmd without
        // standing up a real game-side Effect subclass per test.
        private sealed class LambdaEffect : Effect
        {
            private readonly Action _act;
            public LambdaEffect(Action act) { _act = act; }
            public override void Run(IEffectScope scope) => _act();
        }

        [Test]
        public void RunEffect_OpensBlockNamedAfterEffect()
        {
            var eng = new GameEngine();
            var caster = eng.EntityStore.Create();
            var target = eng.EntityStore.Create(); target.SetHealth(30);
            int checkpoint = eng.World.Log.Count;

            eng.Stack.Push(new StackFrame(new Fireball(caster, target)));

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
            var caster = eng.EntityStore.Create();
            var target = eng.EntityStore.Create();
            int checkpoint = eng.World.Log.Count;

            eng.Stack.Push(new StackFrame(new Fireball(caster, target)));

            var fireballStart = eng.World.Log.Events.Skip(checkpoint)
                                               .OfType<BlockStartEvent>()
                                               .First(b => b.Name == "Fireball");
            Assert.That(fireballStart.Payload.ContainsKey("Source"), Is.True);
            Assert.That(fireballStart.Payload.ContainsKey("Target"), Is.True);
            Assert.That(fireballStart.Payload["Source"], Is.SameAs(caster));
            Assert.That(fireballStart.Payload["Target"], Is.SameAs(target));
        }

        [Test]
        public void Execute_OpensBlock_AndAppliesMutation()
        {
            var eng = new GameEngine();
            var target = eng.EntityStore.Create(); target.SetHealth(30);
            int checkpoint = eng.World.Log.Count;

            eng.Enqueue(new DealDamage(null, target, 6));

            Assert.That(target.Health(), Is.EqualTo(24));
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
            var target = eng.EntityStore.Create(); target.SetHealth(30);

            bool beforeFired = false, afterFired = false;
            eng.EffectStore.Subscribe<DealDamage>(
                new HookConfig { Phase = HookPhase.Before, Priority = HookPriority.OnEffect },
                cmd => new LambdaEffect(() => beforeFired = true));
            eng.EffectStore.Subscribe<DealDamage>(
                new HookConfig { Phase = HookPhase.After, Priority = HookPriority.Reaction },
                cmd => new LambdaEffect(() => afterFired = true));

            eng.Enqueue(new DealDamage(null, target, 6));

            Assert.That(beforeFired, Is.True);
            Assert.That(afterFired, Is.True);
            Assert.That(target.Health(), Is.EqualTo(24));
        }

        [Test]
        public void Hook_BeforeReplacement_RunsBeforeApply()
        {
            var eng = new GameEngine();
            var target = eng.EntityStore.Create(); target.SetHealth(30);

            // Replacement-band hook zeroes Amount before Apply runs.
            eng.EffectStore.Subscribe<DealDamage>(
                new HookConfig { Phase = HookPhase.Before, Priority = HookPriority.Replacement },
                cmd => new LambdaEffect(() => cmd.Amount = 0));

            eng.Enqueue(new DealDamage(null, target, 6));

            Assert.That(target.Health(), Is.EqualTo(30),
                "Replacement-band Before hook must mutate Amount before Apply.");
        }
    }
}
