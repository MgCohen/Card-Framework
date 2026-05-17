#nullable enable
using System.Linq;
using NUnit.Framework;
using VerticalSlice.Engine.Pipeline;
using VerticalSlice.Engine.Stream;
using VerticalSlice.Mirror;
using VerticalSlice.Sample;

namespace VerticalSlice.Tests
{
    public class ZoneTests
    {
        [Test]
        public void MoveCard_WritesZoneTagToDestinationId()
        {
            var eng = new GameEngine();
            Tags.RegisterIndices(eng.EntityStore);
            var hand = eng.EntityStore.Create();          hand.MarkAsZone("Hand");
            var battlefield = eng.EntityStore.Create();   battlefield.MarkAsZone("Battlefield");
            var card = eng.EntityStore.Create();          card.MarkAsCard();
            card.SetZone(hand);

            eng.Enqueue(new MoveCard(card, battlefield));

            Assert.That(card.ZoneId(), Is.EqualTo(battlefield.Id));
        }

        [Test]
        public void MoveCard_FirstPlacement_EmitsTagSet()
        {
            // Card has never had a zone tag → first MoveCard emits TagSet,
            // not TagChange. Mirror Unapply will REMOVE the key.
            var eng = new GameEngine();
            Tags.RegisterIndices(eng.EntityStore);
            var hand = eng.EntityStore.Create(); hand.MarkAsZone("Hand");
            var card = eng.EntityStore.Create(); card.MarkAsCard();
            // NOTE: card has no Zone tag yet — has never been placed.

            int checkpoint = eng.World.Log.Count;
            eng.Enqueue(new MoveCard(card, hand));

            var zoneEvent = eng.World.Log.Events.Skip(checkpoint)
                              .OfType<TagSetEvent>()
                              .Single(e => e.Key == "zone");
            Assert.That(zoneEvent.Value, Is.EqualTo(hand.Id));
        }

        [Test]
        public void MoveCard_Subsequent_EmitsTagChange()
        {
            var eng = new GameEngine();
            Tags.RegisterIndices(eng.EntityStore);
            var hand = eng.EntityStore.Create(); hand.MarkAsZone("Hand");
            var battlefield = eng.EntityStore.Create(); battlefield.MarkAsZone("Battlefield");
            var card = eng.EntityStore.Create(); card.MarkAsCard();
            card.SetZone(hand);   // first placement (TagSet)

            int checkpoint = eng.World.Log.Count;
            eng.Enqueue(new MoveCard(card, battlefield));

            var zoneEvent = eng.World.Log.Events.Skip(checkpoint)
                              .OfType<TagChangeEvent>()
                              .Single(e => e.Key == "zone");
            Assert.That(zoneEvent.Prev, Is.EqualTo(hand.Id));
            Assert.That(zoneEvent.Next, Is.EqualTo(battlefield.Id));
        }

        [Test]
        public void CardsInZone_ReturnsOnlyCardsTaggedInThatZone()
        {
            var eng = new GameEngine();
            Tags.RegisterIndices(eng.EntityStore);
            var hand = eng.EntityStore.Create(); hand.MarkAsZone("Hand");
            var battlefield = eng.EntityStore.Create(); battlefield.MarkAsZone("Battlefield");

            var c1 = eng.EntityStore.Create(); c1.MarkAsCard(); c1.SetZone(hand);
            var c2 = eng.EntityStore.Create(); c2.MarkAsCard(); c2.SetZone(hand);
            var c3 = eng.EntityStore.Create(); c3.MarkAsCard(); c3.SetZone(battlefield);

            var inHand = eng.EntityStore.CardsIn(hand).Select(e => e.Id).OrderBy(i => i).ToList();
            var onBattlefield = eng.EntityStore.CardsIn(battlefield).Select(e => e.Id).ToList();

            Assert.That(inHand, Is.EqualTo(new[] { c1.Id, c2.Id }));
            Assert.That(onBattlefield, Is.EqualTo(new[] { c3.Id }));
        }

        [Test]
        public void MoveCard_RewindsCleanly_ViaMirror()
        {
            // Move sequence: hand → battlefield → graveyard.
            // Replay-to-head, scrub back, scrub back, scrub forward — mirror's
            // view of the card's zone must track exactly.
            var eng = new GameEngine();
            Tags.RegisterIndices(eng.EntityStore);
            var hand = eng.EntityStore.Create(); hand.MarkAsZone("Hand");
            var battlefield = eng.EntityStore.Create(); battlefield.MarkAsZone("Battlefield");
            var graveyard = eng.EntityStore.Create(); graveyard.MarkAsZone("Graveyard");

            var card = eng.EntityStore.Create(); card.MarkAsCard();
            card.SetZone(hand);
            eng.Enqueue(new MoveCard(card, battlefield));
            eng.Enqueue(new MoveCard(card, graveyard));

            int head = eng.World.Log.Count;
            var mirror = new MirrorWorld();
            var scrub = new ScrubController(mirror, eng.World.Log.Events);

            scrub.GoTo(head);
            Assert.That(mirror.Get(card.Id)!.Tags["zone"], Is.EqualTo(graveyard.Id));

            // Find the second MoveCard's BlockStart and rewind to just before it.
            int secondMoveStart = -1, count = 0;
            for (int i = 0; i < eng.World.Log.Events.Count; i++)
            {
                if (eng.World.Log.Events[i] is BlockStartEvent bs && bs.Name == "MoveCard")
                {
                    if (++count == 2) { secondMoveStart = i; break; }
                }
            }
            Assert.That(secondMoveStart, Is.GreaterThan(0));

            scrub.GoTo(secondMoveStart);
            Assert.That(mirror.Get(card.Id)!.Tags["zone"], Is.EqualTo(battlefield.Id));

            scrub.GoTo(head);
            Assert.That(mirror.Get(card.Id)!.Tags["zone"], Is.EqualTo(graveyard.Id));
        }

        [Test]
        public void Zones_QueryReturnsOnlyZoneEntities()
        {
            var eng = new GameEngine();
            Tags.RegisterIndices(eng.EntityStore);
            var z1 = eng.EntityStore.Create(); z1.MarkAsZone("Z1");
            var z2 = eng.EntityStore.Create(); z2.MarkAsZone("Z2");
            var c  = eng.EntityStore.Create(); c.MarkAsCard();   // not a zone

            var zones = eng.EntityStore.Zones().Select(e => e.Id).OrderBy(i => i).ToList();
            Assert.That(zones, Is.EqualTo(new[] { z1.Id, z2.Id }));
        }
    }
}
