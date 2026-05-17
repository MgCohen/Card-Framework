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
        // The entity-storage tests previously poked at a bare World; storage
        // moved onto EntityStore (extraction spike), so the tests now exercise
        // the store directly with a private log.

        private static EntityStore NewStore(out World world)
        {
            world = new World();
            return new EntityStore(world.Log);
        }

        [Test]
        public void Create_AssignsMonotonicIds_StartingAt1()
        {
            var store = NewStore(out _);
            var a = store.Create();
            var b = store.Create();
            Assert.That(a.Id, Is.EqualTo(1));
            Assert.That(b.Id, Is.EqualTo(2));
        }

        [Test]
        public void Create_EmitsEntityCreatedEvent()
        {
            var store = NewStore(out var w);
            store.Create();
            var ev = w.Log.Events.Single();
            Assert.That(ev, Is.TypeOf<EntityCreatedEvent>());
            Assert.That(((EntityCreatedEvent)ev).EntityId, Is.EqualTo(1));
        }

        [Test]
        public void FirstWrite_EmitsTagSet_NotTagChange()
        {
            var store = NewStore(out var w);
            var e = store.Create();
            int checkpoint = w.Log.Count;

            e.SetHealth(30);

            var ev = w.Log.Events.Skip(checkpoint).Single();
            Assert.That(ev, Is.TypeOf<TagSetEvent>());
            var ts = (TagSetEvent)ev;
            Assert.That(ts.EntityId, Is.EqualTo(e.Id));
            Assert.That(ts.Key, Is.EqualTo("health"));
            Assert.That(ts.Value, Is.EqualTo(30));
        }

        [Test]
        public void SecondWrite_EmitsTagChange_WithPrevAndNext()
        {
            var store = NewStore(out var w);
            var e = store.Create();
            e.SetHealth(30);
            int checkpoint = w.Log.Count;

            e.SetHealth(25);

            var ev = w.Log.Events.Skip(checkpoint).Single();
            Assert.That(ev, Is.TypeOf<TagChangeEvent>());
            var tc = (TagChangeEvent)ev;
            Assert.That(tc.Prev, Is.EqualTo(30));
            Assert.That(tc.Next, Is.EqualTo(25));
        }

        [Test]
        public void NoOpWrite_EmitsNothing()
        {
            var store = NewStore(out var w);
            var e = store.Create();
            e.SetHealth(30);
            int checkpoint = w.Log.Count;

            e.SetHealth(30);   // same value

            Assert.That(w.Log.Count, Is.EqualTo(checkpoint),
                "Equality-skip must prevent any event for a no-op write.");
        }

        [Test]
        public void Destroy_EmitsEntityDestroyed_AndStopsTagEvents()
        {
            var store = NewStore(out var w);
            var e = store.Create();
            e.SetHealth(30);
            int checkpoint = w.Log.Count;

            store.Destroy(e);

            var ev = w.Log.Events.Skip(checkpoint).Single();
            Assert.That(ev, Is.TypeOf<EntityDestroyedEvent>());
            Assert.That(store.Get(e.Id), Is.Null);

            int afterDestroy = w.Log.Count;
            e.SetHealth(99);
            Assert.That(w.Log.Count, Is.EqualTo(afterDestroy),
                "Detached entity must not emit further wire events.");
        }

        [Test]
        public void WhereMarker_FiltersByTag()
        {
            var store = NewStore(out _);
            store.IndexMarker(Tags.IsCard);

            var card = store.Create(); card.MarkAsCard();
            store.Create();   // no marker

            var cards = store.With(Tags.IsCard).ToList();
            Assert.That(cards, Has.Count.EqualTo(1));
            Assert.That(cards[0].Id, Is.EqualTo(card.Id));
        }
    }
}
