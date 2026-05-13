#nullable enable
using System.Linq;
using NUnit.Framework;
using VerticalSlice.Engine.Pipeline;
using VerticalSlice.Engine.Stream;
using VerticalSlice.Sample;

namespace VerticalSlice.Tests
{
    public class EngineTests
    {
        [Test]
        public void EntityCreation_AssignsMonotonicIds_StartingAt1()
        {
            var w = new World();
            var a = w.Create<Card>();
            var b = w.Create<Card>();

            Assert.That(a.Id, Is.EqualTo(1));
            Assert.That(b.Id, Is.EqualTo(2));
        }

        [Test]
        public void EntityCreation_WrapsInSystemBlock()
        {
            var w = new World();
            w.Create<Card>();

            var events = w.EventLog.Events;
            Assert.That(events.Count, Is.EqualTo(3));
            Assert.That(events[0], Is.InstanceOf<BlockStartEvent>());
            Assert.That(((BlockStartEvent)events[0]).Type, Is.SameAs(BlockType.System));
            Assert.That(events[1], Is.InstanceOf<EntityCreatedEvent>());
            Assert.That(events[2], Is.InstanceOf<BlockEndEvent>());
        }

        [Test]
        public void TagSet_OutsideExecute_AutoWrapsInSystemBlock()
        {
            var w = new World();
            var c = w.Create<Card>();
            var before = w.EventLog.Count;

            c.Hp = 30;

            var added = w.EventLog.Events.Skip(before).ToList();
            Assert.That(added[0], Is.InstanceOf<BlockStartEvent>());
            Assert.That(((BlockStartEvent)added[0]).Type, Is.SameAs(BlockType.System));
            Assert.That(added[1], Is.InstanceOf<TagChangeEvent>());
            Assert.That(added[2], Is.InstanceOf<BlockEndEvent>());

            var tc = (TagChangeEvent)added[1];
            Assert.That(tc.Key, Is.EqualTo("hp"));
            Assert.That(tc.Prev, Is.EqualTo(0));
            Assert.That(tc.Next, Is.EqualTo(30));
        }

        [Test]
        public void TagSet_NoOp_DoesNotEmit()
        {
            var w = new World();
            var c = w.Create<Card>();
            c.Hp = 30;
            var checkpoint = w.EventLog.Count;

            c.Hp = 30;

            Assert.That(w.EventLog.Count, Is.EqualTo(checkpoint));
        }

        [Test]
        public void Execute_OpensTypedBlock_AndEmitsTagChangesInside()
        {
            var w = new World();
            var c = w.Create<Card>();
            c.Hp = 30;
            var checkpoint = w.EventLog.Count;

            w.Execute(new DealDamage(source: null, target: c, amount: 6));

            var added = w.EventLog.Events.Skip(checkpoint).ToList();
            Assert.That(added[0], Is.InstanceOf<BlockStartEvent>());
            Assert.That(((BlockStartEvent)added[0]).Type, Is.SameAs(BlockType.Power));
            Assert.That(((BlockStartEvent)added[0]).CommandTypeName, Is.EqualTo("DealDamage"));
            Assert.That(added.OfType<TagChangeEvent>().Count(), Is.EqualTo(1));
            Assert.That(added[^1], Is.InstanceOf<BlockEndEvent>());

            Assert.That(c.Hp, Is.EqualTo(24));
        }

        [Test]
        public void Hooks_BeforeAndAfter_BothFire()
        {
            var w = new World();
            var c = w.Create<Card>();
            c.Hp = 30;

            bool beforeFired = false, afterFired = false;
            w.Hooks.Subscribe<DealDamage>(HookPhase.Before, HookPriority.OnEffect,
                _ => beforeFired = true);
            w.Hooks.Subscribe<DealDamage>(HookPhase.After, HookPriority.Reaction,
                _ => afterFired = true);

            w.Execute(new DealDamage(null, c, 6));

            Assert.That(beforeFired, Is.True);
            Assert.That(afterFired, Is.True);
        }

        [Test]
        public void Hooks_Replacement_RunsBeforeOnEffect()
        {
            var w = new World();
            var c = w.Create<Card>();
            c.Hp = 30;

            var order = new System.Collections.Generic.List<string>();
            w.Hooks.Subscribe<DealDamage>(HookPhase.Before, HookPriority.OnEffect,
                _ => order.Add("OnEffect"));
            w.Hooks.Subscribe<DealDamage>(HookPhase.Before, HookPriority.Replacement,
                _ => order.Add("Replacement"));

            w.Execute(new DealDamage(null, c, 6));

            Assert.That(order, Is.EqualTo(new[] { "Replacement", "OnEffect" }));
        }
    }
}
