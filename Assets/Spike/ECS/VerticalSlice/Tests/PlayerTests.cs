#nullable enable
using System.Linq;
using NUnit.Framework;
using VerticalSlice.Engine.Pipeline;
using VerticalSlice.Sample;

namespace VerticalSlice.Tests
{
    public class PlayerTests
    {
        private static EntityStore NewStore()
        {
            var store = new EntityStore(new VerticalSlice.Engine.Stream.EventLog());
            Tags.RegisterIndices(store);
            return store;
        }

        [Test]
        public void MarkAsPlayer_SetsMarkerAndName()
        {
            var store = NewStore();
            var p = store.Create();
            p.MarkAsPlayer("Alice");

            Assert.That(p.IsPlayer(), Is.True);
            Assert.That(p.PlayerName(), Is.EqualTo("Alice"));
        }

        [Test]
        public void Players_QueryReturnsOnlyPlayerEntities()
        {
            var store = NewStore();
            var p1 = store.Create(); p1.MarkAsPlayer("P1");
            var p2 = store.Create(); p2.MarkAsPlayer("P2");
            store.Create();   // unmarked

            var players = store.Players().Select(e => e.Id).OrderBy(i => i).ToList();
            Assert.That(players, Is.EqualTo(new[] { p1.Id, p2.Id }));
        }

        [Test]
        public void SetOwner_AndOwnerId_RoundTrip()
        {
            var store = NewStore();
            var p = store.Create(); p.MarkAsPlayer("P");
            var c = store.Create(); c.MarkAsCard();

            c.SetOwner(p);

            Assert.That(c.OwnerId(), Is.EqualTo(p.Id));
        }

        [Test]
        public void CardsOwnedBy_FiltersByOwnerTag()
        {
            var store = NewStore();
            var p1 = store.Create(); p1.MarkAsPlayer("P1");
            var p2 = store.Create(); p2.MarkAsPlayer("P2");

            var c1 = store.Create(); c1.MarkAsCard(); c1.SetOwner(p1);
            var c2 = store.Create(); c2.MarkAsCard(); c2.SetOwner(p1);
            var c3 = store.Create(); c3.MarkAsCard(); c3.SetOwner(p2);

            var p1Cards = store.CardsOwnedBy(p1).Select(e => e.Id).OrderBy(i => i).ToList();
            var p2Cards = store.CardsOwnedBy(p2).Select(e => e.Id).ToList();

            Assert.That(p1Cards, Is.EqualTo(new[] { c1.Id, c2.Id }));
            Assert.That(p2Cards, Is.EqualTo(new[] { c3.Id }));
        }

        [Test]
        public void CardsNotOwnedBy_ExcludesUnownedCards()
        {
            // The "enemy" view should ignore unowned cards (Owner = 0) so a
            // damaging deathrattle doesn't accidentally hit neutral entities.
            var store = NewStore();
            var p1 = store.Create(); p1.MarkAsPlayer("P1");
            var p2 = store.Create(); p2.MarkAsPlayer("P2");

            var owned1 = store.Create(); owned1.MarkAsCard(); owned1.SetOwner(p1);
            var owned2 = store.Create(); owned2.MarkAsCard(); owned2.SetOwner(p2);
            var unowned = store.Create(); unowned.MarkAsCard();   // no owner

            var enemiesOfP1 = store.CardsNotOwnedBy(p1).Select(e => e.Id).ToList();
            Assert.That(enemiesOfP1, Has.Count.EqualTo(1));
            Assert.That(enemiesOfP1[0], Is.EqualTo(owned2.Id));
            Assert.That(enemiesOfP1.Contains(unowned.Id), Is.False,
                "Unowned cards must be excluded from CardsNotOwnedBy.");
        }
    }
}
