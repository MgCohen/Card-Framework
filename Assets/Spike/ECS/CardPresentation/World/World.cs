#nullable enable
using System;
using System.Collections.Generic;

namespace Spike.CardPresentation
{
    /// <summary>
    /// Authoritative game state — the source of truth for cards and their
    /// zone membership. Owns one <see cref="CardViewModel"/> per entity
    /// (lazily created via <see cref="GetVM"/>) and fires
    /// <see cref="CardChanged"/> on every mutation.
    ///
    /// <para>Bottom of the reactive flow:</para>
    /// <code>
    /// view → intent → VM.RequestMove → GameSystem.MoveCard → World.SetCardZone
    ///                                                          ↓ CardChanged
    ///                                              zones re-bind → views update
    /// </code>
    ///
    /// <para>Mutations only land here through <see cref="GameSystem"/> in
    /// the eventual game — for the spike we don't enforce that, but the
    /// shape is set up so the system would be the only writer.</para>
    /// </summary>
    public sealed class World
    {
        private readonly Dictionary<int, CardEntity> _cards = new();
        private readonly Dictionary<int, CardViewModel> _vms = new();
        private int _nextId = 1;

        /// <summary>Fires when any card mutates (zone change, field change).</summary>
        public event Action<CardEntity>? CardChanged;

        /// <summary>
        /// System reference — used by <see cref="GetVM"/> to inject the
        /// intent router into freshly-created VMs. Set by the sandbox
        /// driver after constructing both.
        /// </summary>
        public GameSystem? System;

        public CardEntity AddCard(CardEntity card)
        {
            if (card.Id == 0) card.Id = _nextId++;
            else _nextId = Math.Max(_nextId, card.Id + 1);
            _cards[card.Id] = card;
            return card;
        }

        public CardEntity? GetCard(int id) =>
            _cards.TryGetValue(id, out var c) ? c : null;

        /// <summary>All cards whose <see cref="CardEntity.ZoneId"/> matches.</summary>
        public IEnumerable<CardEntity> CardsInZone(int zoneId)
        {
            foreach (var c in _cards.Values)
                if (c.ZoneId == zoneId) yield return c;
        }

        /// <summary>
        /// Get the canonical VM for a card. Created on first call and
        /// reused thereafter; refreshed from the entity each call so the
        /// VM always reflects current world state.
        /// </summary>
        public CardViewModel GetVM(int cardId)
        {
            if (!_vms.TryGetValue(cardId, out var vm))
            {
                vm = new CardViewModel { CardId = cardId, System = System };
                _vms[cardId] = vm;
            }
            if (_cards.TryGetValue(cardId, out var entity))
                vm.Refresh(entity);
            return vm;
        }

        /// <summary>
        /// Mutate a card's zone. Primitive write — <see cref="GameSystem"/>
        /// calls this after validating intent. Fires <see cref="CardChanged"/>.
        /// </summary>
        public void SetCardZone(int cardId, int targetZoneId)
        {
            if (!_cards.TryGetValue(cardId, out var card)) return;
            if (card.ZoneId == targetZoneId) return;
            card.ZoneId = targetZoneId;
            GetVM(cardId);   // refresh VM mirror
            CardChanged?.Invoke(card);
        }
    }
}
