#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Spike.CardPresentation
{
    /// <summary>How cards are arranged within the zone's card container.</summary>
    public enum ZoneLayout
    {
        /// <summary>HorizontalLayoutGroup; cards spread side-by-side.</summary>
        Spread,
        /// <summary>All cards at one anchor with a small per-index offset (deck pile).</summary>
        Stack,
        /// <summary>No layout enforced — cards keep whatever position they're given.</summary>
        Free,
    }

    /// <summary>Default face state for cards in this zone (VM.Revealed overrides).</summary>
    public enum ZoneVisibility { Visible, Hidden }

    /// <summary>Behavior on drag release.</summary>
    public enum ZoneAnchoring
    {
        /// <summary>Card stays wherever it was dropped.</summary>
        Free,
        /// <summary>Card snaps back to its home slot in this zone.</summary>
        AnchorBack,
    }

    /// <summary>
    /// Abstract base for a zone. Reactive: observes a <see cref="World"/>
    /// for <see cref="World.CardChanged"/> events and rebuilds its card
    /// list whenever the world mutates. Subclasses exist as named prefab
    /// presets via <see cref="ZoneLayout"/> + visibility + behavior bools
    /// configured in their <c>Reset()</c>.
    ///
    /// <para><b>Flow.</b> Drops here route through the dragged card's VM,
    /// not direct zone-to-zone calls:</para>
    /// <code>
    /// OnDrop → dragged.Model.RequestMove(ZoneId) → System.MoveCard
    ///        → World.SetCardZone → CardChanged → Rebind on all zones
    ///        → smart diff updates _views
    /// </code>
    /// </summary>
    public abstract class CardZone : MonoBehaviour, IDropHandler
    {
        [Header("Identity")]
        [Tooltip("Stable id assigned by the sandbox at wire-up. Cards with " +
                 "matching CardEntity.ZoneId belong to this zone.")]
        public int ZoneId;

        [Header("References")]
        [Tooltip("Card prefab this zone spawns. Per-zone — when a card moves between zones, " +
                 "the destination instantiates its own prefab, so a card can have one visual " +
                 "in the hand and another in the pile.")]
        [SerializeField] protected UICardView _cardPrefab = null!;

        [Tooltip("Where cards spawn as children. Falls back to the zone transform.")]
        [SerializeField] protected Transform? _cardContainer;

        [Tooltip("Canvas the card reparents under during drag.")]
        [SerializeField] protected Canvas? _dragCanvas;

        [Header("Label")]
        [SerializeField] protected string _label = "";
        [SerializeField] protected Text? _labelText;

        [Header("Layout")]
        [SerializeField] protected ZoneLayout _layout = ZoneLayout.Spread;
        [Tooltip("Stack layout only — per-card offset applied progressively.")]
        [SerializeField] protected Vector2 _stackOffset = new Vector2(2f, -2f);

        [Header("Card behaviors (defaults; VM intrinsics override)")]
        [SerializeField] protected ZoneVisibility _visibility = ZoneVisibility.Visible;
        [SerializeField] protected bool _draggable = true;
        [SerializeField] protected bool _flippable = false;
        [SerializeField] protected bool _droppable = false;
        [SerializeField] protected ZoneAnchoring _anchoring = ZoneAnchoring.AnchorBack;
        [SerializeField] protected bool _ghostAnchor = false;

        [Header("Capacity")]
        [Tooltip("Maximum number of cards this zone will hold. -1 = unlimited.")]
        [SerializeField] protected int _maxCards = -1;

        private readonly List<UICardView> _views = new();
        private World? _world;
        private GameSystem? _system;

        /// <summary>Cards currently displayed in this zone, in slot order.</summary>
        public IReadOnlyList<UICardView> Views => _views;

        public bool Droppable => _droppable;
        public int MaxCards => _maxCards;

        /// <summary>
        /// Per-card drop validation hook. AND-combines three gates: the
        /// zone's <see cref="Droppable"/> flag, the capacity limit, and
        /// the system's authoritative valid-actions list (via
        /// <see cref="CardViewModel.CanMoveTo"/>). All three must agree
        /// for the drop to be allowed; subclasses can override for
        /// additional kind-specific rules.
        /// </summary>
        public virtual bool CanAccept(CardViewModel vm)
        {
            if (!_droppable) return false;
            if (_maxCards >= 0 && _views.Count >= _maxCards) return false;
            if (!vm.CanMoveTo(ZoneId)) return false;
            return true;
        }

        protected virtual void Awake()
        {
            if (_labelText != null) _labelText.text = _label;

            // HLG on/off per layout — prefab always has one, we toggle here.
            var track = _cardContainer != null ? _cardContainer : transform;
            var hlg = track.GetComponent<HorizontalLayoutGroup>();
            if (hlg != null) hlg.enabled = _layout == ZoneLayout.Spread;
        }

        protected virtual void OnDestroy()
        {
            if (_world != null) _world.CardChanged -= OnWorldCardChanged;
            if (_system != null) _system.ValidActionsChanged -= OnValidActionsChanged;
        }

        /// <summary>
        /// Wire to the World+System. Subscribes to world changes + valid-
        /// actions changes, sets this zone's id, and does the initial bind.
        /// Sandbox calls this in Start() so all Awakes have settled.
        /// </summary>
        public void SetWorld(World world, GameSystem system, int zoneId)
        {
            if (_world != null) _world.CardChanged -= OnWorldCardChanged;
            if (_system != null) _system.ValidActionsChanged -= OnValidActionsChanged;
            _world = world;
            _system = system;
            ZoneId = zoneId;
            _world.CardChanged += OnWorldCardChanged;
            _system.ValidActionsChanged += OnValidActionsChanged;
            Rebind();
        }

        private void OnWorldCardChanged(CardEntity card) => Rebind();

        /// <summary>
        /// Re-derive per-card gates without rebuilding views. Cheap path —
        /// only the toggles (Draggable, Actionable) depend on the action
        /// list, so we don't need a full diff-rebind.
        /// </summary>
        private void OnValidActionsChanged()
        {
            foreach (var view in _views)
            {
                if (view.Model == null) continue;
                ConfigureCard(view, view.Model);
            }
        }

        /// <summary>
        /// Pull the current set of cards-in-this-zone from the world and
        /// diff against existing views: keep views whose card is still
        /// here, spawn for newcomers, destroy departures (skipping any
        /// view marked <see cref="UICardView.Claimed"/> — those are
        /// mid-drag and will self-destruct on OnEndDrag).
        /// </summary>
        private void Rebind()
        {
            if (_world == null) return;

            var existing = new Dictionary<int, UICardView>(_views.Count);
            foreach (var v in _views)
                if (v.Model != null) existing[v.Model.CardId] = v;
            _views.Clear();

            int index = 0;
            foreach (var entity in _world.CardsInZone(ZoneId))
            {
                var vm = _world.GetVM(entity.Id);
                UICardView view;
                if (existing.TryGetValue(entity.Id, out var keep))
                {
                    // Card was here before, still here — keep view, refresh.
                    view = keep;
                    view.Bind(vm);
                    ConfigureCard(view, vm);
                    existing.Remove(entity.Id);
                }
                else
                {
                    // New arrival — spawn from this zone's prefab.
                    view = CreateView(vm);
                }
                view.transform.SetSiblingIndex(index);
                _views.Add(view);
                OnCardSpawned(view, vm, index);
                index++;
            }

            // Cards no longer in this zone — destroy their views (unless
            // mid-drag — let OnEndDrag handle those).
            foreach (var leaver in existing.Values)
            {
                if (leaver.Claimed) continue;
                leaver.Unbind();
                Destroy(leaver.gameObject);
            }

            Relayout();
        }

        private UICardView CreateView(CardViewModel vm)
        {
            var parent = _cardContainer != null ? _cardContainer : transform;
            var view = Instantiate(_cardPrefab, parent);
            view.SetCanvas(_dragCanvas!);
            view.OwningZone = this;
            view.Bind(vm);
            ConfigureCard(view, vm);
            return view;
        }

        /// <summary>
        /// Combine zone defaults with VM intrinsics and the system's
        /// valid-action list, write to the card's public toggles.
        /// <c>Draggable</c> and <c>Actionable</c> are AND-gated against
        /// <see cref="CardViewModel.HasAnyMove"/> — a card the system says
        /// is immobile is dim and won't initiate drag, regardless of zone
        /// defaults.
        /// </summary>
        protected virtual void ConfigureCard(UICardView view, CardViewModel vm)
        {
            bool hasMove = vm.HasAnyMove();
            view.Draggable   = hasMove && _draggable && !vm.Frozen;
            view.Actionable  = hasMove;
            view.AnchorsBack = (_anchoring == ZoneAnchoring.AnchorBack) || vm.Pinned;
            view.FaceDown    = (_visibility == ZoneVisibility.Hidden) && !vm.Revealed;
            view.Flippable   = _flippable;
            view.GhostAnchor = _ghostAnchor;
        }

        /// <summary>
        /// Variant hook called after each card is spawned (or rebound).
        /// Base implements Stack-layout positioning.
        /// </summary>
        protected virtual void OnCardSpawned(UICardView view, CardViewModel vm, int index)
        {
            if (_layout == ZoneLayout.Stack)
            {
                var rt = (RectTransform)view.transform;
                rt.anchoredPosition = _stackOffset * index;
            }
        }

        /// <summary>
        /// Variant hook for whole-set rules. Base only handles Stack
        /// repositioning — the "only top is draggable" rule has moved
        /// into <see cref="GameSystem"/>'s valid-actions enumeration, so
        /// non-top stack cards naturally have <see cref="UICardView.Draggable"/>
        /// off via <see cref="ConfigureCard"/>.
        /// </summary>
        protected virtual void Relayout()
        {
            if (_layout == ZoneLayout.Stack)
            {
                for (int i = 0; i < _views.Count; i++)
                {
                    var rt = (RectTransform)_views[i].transform;
                    rt.anchoredPosition = _stackOffset * i;
                }
            }
        }

        /// <summary>
        /// IDropHandler — when a card is released over this zone's
        /// hierarchy, uGUI bubbles the event up to here. Emit an intent
        /// through the dragged card's VM; the resulting world update
        /// causes the actual zone-transfer via <see cref="OnWorldCardChanged"/>.
        /// </summary>
        public void OnDrop(PointerEventData e)
        {
            var dragged = e.pointerDrag != null
                ? e.pointerDrag.GetComponent<UICardView>() : null;
            if (dragged == null || dragged.Model == null) return;
            if (!dragged.IsDragging) return;

            var source = dragged.OwningZone;
            if (source == null || source == this) return;

            if (!CanAccept(dragged.Model)) return;

            // Claim first — the source zone's Rebind will see this view as
            // a leaver and skip destroying it, letting its OnEndDrag handle
            // the cleanup.
            dragged.Claimed = true;
            dragged.Model.RequestMove(ZoneId);
        }
    }
}
