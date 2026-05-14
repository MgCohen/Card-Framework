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
        public void Create_AssignsMonotonicIds_StartingAt1()
        {
            var w = new World();
            var a = w.Create();
            var b = w.Create();
            Assert.That(a.Id, Is.EqualTo(1));
            Assert.That(b.Id, Is.EqualTo(2));
        }

        [Test]
        public void Create_EmitsEntityCreatedEvent()
        {
            var w = new World();
            w.Create();
            var ev = w.Log.Events.Single();
            Assert.That(ev, Is.TypeOf<EntityCreatedEvent>());
            Assert.That(((EntityCreatedEvent)ev).EntityId, Is.EqualTo(1));
        }

        [Test]
        public void FirstWrite_EmitsTagSet_NotTagChange()
        {
            var w = new World();
            var e = w.Create();
            int checkpoint = w.Log.Count;

            e.SetHp(30);

            var ev = w.Log.Events.Skip(checkpoint).Single();
            Assert.That(ev, Is.TypeOf<TagSetEvent>());
            var ts = (TagSetEvent)ev;
            Assert.That(ts.EntityId, Is.EqualTo(e.Id));
            Assert.That(ts.Key, Is.EqualTo("hp"));
            Assert.That(ts.Value, Is.EqualTo(30));
        }

        [Test]
        public void SecondWrite_EmitsTagChange_WithPrevAndNext()
        {
            var w = new World();
            var e = w.Create();
            e.SetHp(30);
            int checkpoint = w.Log.Count;

            e.SetHp(25);

            var ev = w.Log.Events.Skip(checkpoint).Single();
            Assert.That(ev, Is.TypeOf<TagChangeEvent>());
            var tc = (TagChangeEvent)ev;
            Assert.That(tc.Prev, Is.EqualTo(30));
            Assert.That(tc.Next, Is.EqualTo(25));
        }

        [Test]
        public void NoOpWrite_EmitsNothing()
        {
            var w = new World();
            var e = w.Create();
            e.SetHp(30);
            int checkpoint = w.Log.Count;

            e.SetHp(30);   // same value

            Assert.That(w.Log.Count, Is.EqualTo(checkpoint),
                "Equality-skip must prevent any event for a no-op write.");
        }

        [Test]
        public void Destroy_EmitsEntityDestroyed_AndStopsTagEvents()
        {
            var w = new World();
            var e = w.Create();
            e.SetHp(30);
            int checkpoint = w.Log.Count;

            w.Destroy(e);

            var ev = w.Log.Events.Skip(checkpoint).Single();
            Assert.That(ev, Is.TypeOf<EntityDestroyedEvent>());
            Assert.That(w.Get(e.Id), Is.Null);

            int afterDestroy = w.Log.Count;
            e.SetHp(99);
            Assert.That(w.Log.Count, Is.EqualTo(afterDestroy),
                "Detached entity must not emit further wire events.");
        }

        [Test]
        public void WhereMarker_FiltersByTag()
        {
            var w = new World();
            var card = w.Create(); card.MarkAsCard();
            w.Create();   // no marker

            var cards = w.Where(Tags.IsCard).ToList();
            Assert.That(cards, Has.Count.EqualTo(1));
            Assert.That(cards[0].Id, Is.EqualTo(card.Id));
        }
    }
}
