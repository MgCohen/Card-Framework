#nullable enable
using System;
using UnityEngine;

namespace Spike.CardPresentation
{
    public enum CardKind { Minion, Spell, Hero, Other }

    /// <summary>
    /// Reactive projection of a <see cref="CardEntity"/>. Mirrors the
    /// entity's display fields and intrinsic flags; views subscribe to
    /// <see cref="Changed"/> for repaint signals.
    ///
    /// <para>The VM is also the input-routing surface: drops on a zone
    /// call <see cref="RequestMove"/> on the dragged card's VM, which
    /// forwards to the held <see cref="System"/>. Keeps views ignorant
    /// of how intents reach the world.</para>
    ///
    /// <para>One VM per card-id, owned by <see cref="World"/>. Multiple
    /// views can bind to the same VM if needed (e.g. a card visible in
    /// two windows); all repaint together.</para>
    /// </summary>
    public sealed class CardViewModel
    {
        /// <summary>Identity. Matches <see cref="CardEntity.Id"/>.</summary>
        public int CardId;

        /// <summary>Intent router. Injected by <see cref="World.GetVM"/>.</summary>
        public GameSystem? System;

        /// <summary>Single coarse change signal — views re-bind on each fire.</summary>
        public event Action? Changed;

        // --- Display fields — mirrored from CardEntity in Refresh() ---

        private string _name = "";
        public string Name { get => _name; set => Set(ref _name, value); }

        private int _cost;
        public int Cost { get => _cost; set => Set(ref _cost, value); }

        private CardKind _kind = CardKind.Other;
        public CardKind Kind { get => _kind; set => Set(ref _kind, value); }

        private int? _attack;
        public int? Attack { get => _attack; set => Set(ref _attack, value); }

        private int? _health;
        public int? Health { get => _health; set => Set(ref _health, value); }

        private string _bodyText = "";
        public string BodyText { get => _bodyText; set => Set(ref _bodyText, value); }

        private Sprite? _art;
        public Sprite? Art { get => _art; set => Set(ref _art, value); }

        // --- Intrinsic overrides — override zone defaults ---

        private bool _revealed;
        /// <summary>Force face-up even when the zone defaults to hidden.</summary>
        public bool Revealed { get => _revealed; set => Set(ref _revealed, value); }

        private bool _frozen;
        /// <summary>Force non-draggable even when the zone defaults to draggable.</summary>
        public bool Frozen { get => _frozen; set => Set(ref _frozen, value); }

        private bool _pinned;
        /// <summary>Force anchors-back even when the zone is free-drop.</summary>
        public bool Pinned { get => _pinned; set => Set(ref _pinned, value); }

        /// <summary>Mirror an entity's current state. Setters fire Changed when values differ.</summary>
        public void Refresh(CardEntity e)
        {
            Name      = e.Name;
            Kind      = e.Kind;
            Cost      = e.Cost;
            Attack    = e.Attack;
            Health    = e.Health;
            BodyText  = e.BodyText;
            Revealed  = e.Revealed;
            Frozen    = e.Frozen;
            Pinned    = e.Pinned;
        }

        /// <summary>
        /// Request this card move to a target zone. The system matches the
        /// intent against its <see cref="GameSystem.ValidActions"/> list;
        /// accepts iff there's a corresponding entry, silently rejects
        /// otherwise. View should typically check <see cref="CanMoveTo"/>
        /// first, but TrySubmit is safe to call regardless.
        /// </summary>
        public bool RequestMove(int targetZoneId) =>
            System?.TrySubmit(CardId, targetZoneId) ?? false;

        /// <summary>Is this card legally movable to the given zone right now?</summary>
        public bool CanMoveTo(int targetZoneId) =>
            System != null && System.CanMove(CardId, targetZoneId);

        /// <summary>Does this card have any legal move at all? Used for highlighting.</summary>
        public bool HasAnyMove() =>
            System != null && System.HasAnyMove(CardId);

        private void Set<T>(ref T field, T value)
        {
            if (Equals(field, value)) return;
            field = value;
            Changed?.Invoke();
        }
    }
}
