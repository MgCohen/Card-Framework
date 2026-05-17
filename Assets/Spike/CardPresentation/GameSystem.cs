#nullable enable
using System;
using System.Collections.Generic;

namespace Spike.CardPresentation
{
    /// <summary>
    /// Rule + intent layer. Authoritative on what moves are currently
    /// legal — publishes a <see cref="ValidActions"/> list that VMs consult
    /// to decide what to highlight and what drops to accept. Recomputes the
    /// list on every world change.
    ///
    /// <para>Player intents (drop gestures) are accepted via
    /// <see cref="TrySubmit"/>: if the requested move exists in
    /// <see cref="ValidActions"/>, it lands as a world mutation; otherwise
    /// silently rejected. The VM/view layer never writes to the world
    /// directly — every change flows through here.</para>
    ///
    /// <para>For the spike, rules are hard-coded zone-pair allow-lists:
    /// see <see cref="ValidDestinationsFor"/>. Real rule logic (turn,
    /// cost, restrictions) would replace that method.</para>
    /// </summary>
    public sealed class GameSystem
    {
        // Zone identifiers — must match what PresentationSandbox assigns.
        // Duplicated here for the demo rule-encoding; in a real wire-up
        // they'd come from a registry or the world's zone definitions.
        public const int ZoneIdDeck      = 1;
        public const int ZoneIdHand      = 2;
        public const int ZoneIdPlayfield = 3;
        public const int ZoneIdGraveyard = 4;

        private readonly World _world;
        private readonly List<MoveAction> _actions = new();

        /// <summary>Fires when <see cref="ValidActions"/> may have changed.</summary>
        public event Action? ValidActionsChanged;

        /// <summary>Currently legal moves. Subscribers re-query for highlighting + drop gating.</summary>
        public IReadOnlyList<MoveAction> ValidActions => _actions;

        public GameSystem(World world)
        {
            _world = world;
            _world.CardChanged += OnWorldCardChanged;
        }

        private void OnWorldCardChanged(CardEntity _) => Recompute();

        /// <summary>
        /// Submit a player intent. Match against <see cref="ValidActions"/>;
        /// if present, mutate the world. Returns true on accept, false on
        /// silent reject (rule out, capacity full, etc.).
        /// </summary>
        public bool TrySubmit(int cardId, int targetZoneId)
        {
            if (!CanMove(cardId, targetZoneId)) return false;
            _world.SetCardZone(cardId, targetZoneId);
            return true;
        }

        /// <summary>Is "move <paramref name="cardId"/> to <paramref name="targetZoneId"/>" currently legal?</summary>
        public bool CanMove(int cardId, int targetZoneId)
        {
            foreach (var a in _actions)
                if (a.CardId == cardId && a.ToZoneId == targetZoneId)
                    return true;
            return false;
        }

        /// <summary>Does this card have <em>any</em> legal move? Used for highlighting.</summary>
        public bool HasAnyMove(int cardId)
        {
            foreach (var a in _actions)
                if (a.CardId == cardId)
                    return true;
            return false;
        }

        /// <summary>
        /// Force a re-evaluation. Called automatically on
        /// <see cref="World.CardChanged"/>; call once explicitly after
        /// seeding the world so the first valid-actions snapshot reflects
        /// the starting state.
        /// </summary>
        public void Recompute()
        {
            _actions.Clear();

            // For each zone, find which cards in it have movement permission,
            // and to which destination zones. Stack-style source zones only
            // expose their top card; spread zones expose everything.
            EnumerateFromStackTop(ZoneIdDeck);
            EnumerateFromSpread  (ZoneIdHand);
            EnumerateFromSpread  (ZoneIdPlayfield);
            EnumerateFromStackTop(ZoneIdGraveyard);

            ValidActionsChanged?.Invoke();
        }

        private void EnumerateFromSpread(int fromZone)
        {
            var dests = ValidDestinationsFor(fromZone);
            if (dests.Length == 0) return;
            foreach (var card in _world.CardsInZone(fromZone))
                foreach (var dest in dests)
                    _actions.Add(new MoveAction(card.Id, fromZone, dest));
        }

        private void EnumerateFromStackTop(int fromZone)
        {
            var dests = ValidDestinationsFor(fromZone);
            if (dests.Length == 0) return;
            CardEntity? top = null;
            // "Top" = last card added. World preserves insertion order on
            // CardsInZone enumeration.
            foreach (var c in _world.CardsInZone(fromZone)) top = c;
            if (top == null) return;
            foreach (var dest in dests)
                _actions.Add(new MoveAction(top.Id, fromZone, dest));
        }

        /// <summary>
        /// Demo rule table — zone-pair allow-list. <em>Replace with real rule
        /// logic when wiring to the engine.</em>
        /// </summary>
        private static int[] ValidDestinationsFor(int fromZoneId) => fromZoneId switch
        {
            // Deck draws to Hand or discards to Graveyard. NOT to Play Field
            // (you don't put unrevealed cards into play).
            ZoneIdDeck      => new[] { ZoneIdHand, ZoneIdGraveyard },

            // Hand plays to the field or discards.
            ZoneIdHand      => new[] { ZoneIdPlayfield, ZoneIdGraveyard },

            // Cards on the field retire to the graveyard.
            ZoneIdPlayfield => new[] { ZoneIdGraveyard },

            // Graveyard is a sink — nothing leaves.
            ZoneIdGraveyard => System.Array.Empty<int>(),

            _ => System.Array.Empty<int>(),
        };
    }
}
