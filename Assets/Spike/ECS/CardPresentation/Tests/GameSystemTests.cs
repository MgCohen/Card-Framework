#nullable enable
using System.Linq;
using NUnit.Framework;

namespace Spike.CardPresentation.Tests
{
    /// <summary>
    /// Rule-table validation for the demo zone-pair allow-list:
    ///   Deck      → Hand, Graveyard       (top-of-stack only)
    ///   Hand      → Playfield, Graveyard  (spread — every card)
    ///   Playfield → Graveyard             (spread — every card)
    ///   Graveyard → ∅
    /// </summary>
    public class GameSystemTests
    {
        private static (World w, GameSystem sys) NewWired()
        {
            var w = new World();
            var sys = new GameSystem(w);
            w.System = sys;
            return (w, sys);
        }

        private static CardEntity AddTo(World w, int zoneId)
            => w.AddCard(new CardEntity { ZoneId = zoneId });

        // ─── ValidActions populated per rule table ────────────────────────

        [Test]
        public void Hand_ExposesEveryCard_ToPlayfieldAndGraveyard()
        {
            var (w, sys) = NewWired();
            var a = AddTo(w, GameSystem.ZoneIdHand);
            var b = AddTo(w, GameSystem.ZoneIdHand);
            sys.Recompute();

            Assert.That(sys.CanMove(a.Id, GameSystem.ZoneIdPlayfield), Is.True);
            Assert.That(sys.CanMove(a.Id, GameSystem.ZoneIdGraveyard), Is.True);
            Assert.That(sys.CanMove(b.Id, GameSystem.ZoneIdPlayfield), Is.True);
            Assert.That(sys.CanMove(b.Id, GameSystem.ZoneIdGraveyard), Is.True);
            // Not allowed by rule table.
            Assert.That(sys.CanMove(a.Id, GameSystem.ZoneIdDeck), Is.False);
        }

        [Test]
        public void Deck_ExposesOnlyTopCard_ToHandAndGraveyard()
        {
            var (w, sys) = NewWired();
            var bottom = AddTo(w, GameSystem.ZoneIdDeck);
            var middle = AddTo(w, GameSystem.ZoneIdDeck);
            var top    = AddTo(w, GameSystem.ZoneIdDeck);
            sys.Recompute();

            // Top — insertion order's last — is the only mover.
            Assert.That(sys.HasAnyMove(top.Id),    Is.True);
            Assert.That(sys.HasAnyMove(middle.Id), Is.False);
            Assert.That(sys.HasAnyMove(bottom.Id), Is.False);

            Assert.That(sys.CanMove(top.Id, GameSystem.ZoneIdHand),      Is.True);
            Assert.That(sys.CanMove(top.Id, GameSystem.ZoneIdGraveyard), Is.True);
            // Deck cards never go straight to the field by rule.
            Assert.That(sys.CanMove(top.Id, GameSystem.ZoneIdPlayfield), Is.False);
        }

        [Test]
        public void Playfield_ExposesEveryCard_ToGraveyardOnly()
        {
            var (w, sys) = NewWired();
            var minion = AddTo(w, GameSystem.ZoneIdPlayfield);
            sys.Recompute();

            Assert.That(sys.CanMove(minion.Id, GameSystem.ZoneIdGraveyard), Is.True);
            Assert.That(sys.CanMove(minion.Id, GameSystem.ZoneIdHand),      Is.False);
            Assert.That(sys.CanMove(minion.Id, GameSystem.ZoneIdDeck),      Is.False);
        }

        [Test]
        public void Graveyard_IsASink_NoMovesOut()
        {
            var (w, sys) = NewWired();
            var dead = AddTo(w, GameSystem.ZoneIdGraveyard);
            sys.Recompute();

            Assert.That(sys.HasAnyMove(dead.Id), Is.False);
        }

        [Test]
        public void EmptyZones_ProduceNoActions()
        {
            var (_, sys) = NewWired();
            sys.Recompute();
            Assert.That(sys.ValidActions.Count, Is.EqualTo(0));
        }

        // ─── TrySubmit ────────────────────────────────────────────────────

        [Test]
        public void TrySubmit_LegalMove_MutatesWorld_AndReturnsTrue()
        {
            var (w, sys) = NewWired();
            var card = AddTo(w, GameSystem.ZoneIdHand);
            sys.Recompute();

            bool ok = sys.TrySubmit(card.Id, GameSystem.ZoneIdPlayfield);

            Assert.That(ok, Is.True);
            Assert.That(card.ZoneId, Is.EqualTo(GameSystem.ZoneIdPlayfield));
        }

        [Test]
        public void TrySubmit_IllegalMove_LeavesZoneUnchanged_AndReturnsFalse()
        {
            var (w, sys) = NewWired();
            var card = AddTo(w, GameSystem.ZoneIdGraveyard);
            sys.Recompute();

            bool ok = sys.TrySubmit(card.Id, GameSystem.ZoneIdHand);

            Assert.That(ok, Is.False);
            Assert.That(card.ZoneId, Is.EqualTo(GameSystem.ZoneIdGraveyard));
        }

        [Test]
        public void TrySubmit_UnknownCard_ReturnsFalse()
        {
            var (_, sys) = NewWired();
            sys.Recompute();
            Assert.That(sys.TrySubmit(cardId: 999, GameSystem.ZoneIdHand), Is.False);
        }

        // ─── Reactive recompute on world change ───────────────────────────

        [Test]
        public void Recompute_FiresValidActionsChanged()
        {
            var (_, sys) = NewWired();
            int fires = 0;
            sys.ValidActionsChanged += () => fires++;
            sys.Recompute();
            Assert.That(fires, Is.EqualTo(1));
        }

        [Test]
        public void WorldMutation_TriggersRecompute_RefreshingValidActions()
        {
            var (w, sys) = NewWired();
            var card = AddTo(w, GameSystem.ZoneIdHand);
            sys.Recompute();
            Assert.That(sys.CanMove(card.Id, GameSystem.ZoneIdPlayfield), Is.True);

            // Move card to graveyard — sink, no further moves.
            w.SetCardZone(card.Id, GameSystem.ZoneIdGraveyard);

            Assert.That(sys.HasAnyMove(card.Id), Is.False);
        }

        [Test]
        public void ValidActions_ContainsExpectedRowsForMixedBoard()
        {
            var (w, sys) = NewWired();
            var inHand     = AddTo(w, GameSystem.ZoneIdHand);
            var onField    = AddTo(w, GameSystem.ZoneIdPlayfield);
            var inGrave    = AddTo(w, GameSystem.ZoneIdGraveyard);
            sys.Recompute();

            var rows = sys.ValidActions.ToList();
            // Hand card: 2 destinations. Playfield card: 1. Graveyard: 0.
            Assert.That(rows.Count, Is.EqualTo(3));
            Assert.That(rows.Any(a => a.CardId == inHand.Id  && a.ToZoneId == GameSystem.ZoneIdPlayfield));
            Assert.That(rows.Any(a => a.CardId == inHand.Id  && a.ToZoneId == GameSystem.ZoneIdGraveyard));
            Assert.That(rows.Any(a => a.CardId == onField.Id && a.ToZoneId == GameSystem.ZoneIdGraveyard));
            Assert.That(rows.All(a => a.CardId != inGrave.Id));
        }

        [Test]
        public void MoveAction_FromZoneId_IsThePresentZone()
        {
            var (w, sys) = NewWired();
            var card = AddTo(w, GameSystem.ZoneIdHand);
            sys.Recompute();

            var action = sys.ValidActions.First(a => a.CardId == card.Id);
            Assert.That(action.FromZoneId, Is.EqualTo(GameSystem.ZoneIdHand));
        }
    }
}
