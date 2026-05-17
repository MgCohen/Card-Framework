#nullable enable
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Spike.CardPresentation
{
    /// <summary>
    /// Prefab-driven uGUI renderer for one card. The visual hierarchy
    /// (cost gem, name strip, art panel, body text, stats, back face) is
    /// authored in the editor and wired via <c>[SerializeField]</c>
    /// references — this script just <em>writes</em> data into pre-placed
    /// components. No layout math in code.
    ///
    /// <para>Behaviors (drag, anchor-back, face/back) are owned by the
    /// card: it implements the input handlers. The <em>configuration</em>
    /// of those toggles comes from the zone that contains it, combining
    /// zone defaults with VM intrinsic flags. The view doesn't know about
    /// zones — it just exposes setters.</para>
    ///
    /// <para>Reactive: <see cref="Bind"/> subscribes to the VM's
    /// <see cref="CardViewModel.Changed"/> event so any mutation of the
    /// VM triggers an immediate re-paint. <see cref="Unbind"/> tears the
    /// subscription down.</para>
    /// </summary>
    public sealed class UICardView : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        // --- Authored references (set on the prefab) ----------------------
        [SerializeField] private Image  _root      = null!;
        [SerializeField] private GameObject _frontFace = null!;
        [SerializeField] private GameObject _backFace  = null!;
        [SerializeField] private Text   _nameText  = null!;
        [SerializeField] private Text   _costText  = null!;
        [SerializeField] private Image  _artPanel  = null!;
        [SerializeField] private Image  _artImage  = null!;
        [SerializeField] private Text   _bodyText  = null!;
        [SerializeField] private Text   _statsText = null!;
        [SerializeField] private CanvasGroup? _canvasGroup;

        // --- Behavior toggles (set by the owning zone) --------------------
        public bool Draggable;
        public bool AnchorsBack = true;
        public bool Flippable;
        public bool GhostAnchor;

        // Highlighting — dim when the system reports the card has no
        // legal moves right now. Driven by ConfigureCard via vm.HasAnyMove.
        private bool _actionable = true;
        public bool Actionable
        {
            get => _actionable;
            set
            {
                _actionable = value;
                if (_canvasGroup != null)
                    _canvasGroup.alpha = _actionable ? 1.0f : 0.45f;
            }
        }

        // --- Cross-zone wiring (set by zone on spawn / by drop handler) ---
        /// <summary>The zone that currently owns this view. Set when spawned.</summary>
        public CardZone? OwningZone { get; set; }

        /// <summary>
        /// Set by a drop receiver when it's about to take ownership of this
        /// view's VM. Tells <see cref="OnEndDrag"/> to skip snap-back and
        /// destroy self — the receiver will spawn a fresh view.
        /// </summary>
        public bool Claimed { get; set; }

        /// <summary>
        /// True only while <see cref="OnBeginDrag"/> actually accepted the
        /// drag (Draggable + not face-down). Drop receivers check this —
        /// uGUI sets <c>PointerEventData.pointerDrag</c> based on interface
        /// implementation alone, so without this check a non-draggable
        /// face-down card would still get "transferred" by the receiver.
        /// </summary>
        public bool IsDragging => _isDragging;

        private bool _faceDown;
        public bool FaceDown
        {
            get => _faceDown;
            set
            {
                _faceDown = value;
                ApplyFaceState();
            }
        }

        public void Flip() => FaceDown = !FaceDown;

        public CardViewModel? Model { get; private set; }
        public event Action<UICardView>? Dropped;

        // Drag state.
        private Canvas? _rootCanvas;
        private Transform? _homeParent;
        private int _homeSiblingIndex;
        private Vector2 _homeAnchoredPos;
        private Vector3 _dragOffset;
        private bool _isDragging;
        private GameObject? _ghost;   // placeholder when GhostAnchor is on

        /// <summary>
        /// Bind to a view-model. Re-callable — subscribes to VM changes
        /// and renders the current state. Pass <c>null</c> to detach.
        /// </summary>
        public void Bind(CardViewModel? vm)
        {
            // Always unsubscribe the previous VM before swapping.
            if (Model != null) Model.Changed -= Repaint;
            Model = vm;
            if (Model != null) Model.Changed += Repaint;
            Repaint();
        }

        /// <summary>Drop the subscription. Call when retiring the view.</summary>
        public void Unbind() => Bind(null);

        private void OnDestroy()
        {
            // Defensive — if the zone forgot to Unbind, don't dangle.
            if (Model != null) Model.Changed -= Repaint;
        }

        /// <summary>Override the canvas used for drag z-order lifting.</summary>
        public void SetCanvas(Canvas canvas) => _rootCanvas = canvas;

        /// <summary>Recolor the card's root surface (used by tinting).</summary>
        public void SetTint(Color tint)
        {
            if (_root != null) _root.color = tint;
        }

        private void Repaint()
        {
            if (Model == null) return;

            _nameText.text = Model.Name;
            _costText.text = Model.Cost.ToString();

            if (Model.Art != null)
            {
                _artImage.sprite = Model.Art;
                _artImage.enabled = true;
            }
            else
            {
                _artImage.enabled = false;
            }

            switch (Model.Kind)
            {
                case CardKind.Minion:
                    _statsText.text = $"{Model.Attack ?? 0} / {Model.Health ?? 0}";
                    _bodyText.text  = Model.BodyText;
                    _artPanel.color = new Color(0.16f, 0.26f, 0.16f);
                    break;
                case CardKind.Spell:
                    _statsText.text = "";
                    _bodyText.text  = string.IsNullOrEmpty(Model.BodyText) ? "spell" : Model.BodyText;
                    _artPanel.color = new Color(0.16f, 0.20f, 0.32f);
                    break;
                case CardKind.Hero:
                    _statsText.text = Model.Health.HasValue ? $"♥ {Model.Health.Value}" : "";
                    _bodyText.text  = Model.BodyText;
                    _artPanel.color = new Color(0.30f, 0.22f, 0.12f);
                    break;
                default:
                    _statsText.text = "";
                    _bodyText.text  = Model.BodyText;
                    _artPanel.color = new Color(0.18f, 0.18f, 0.22f);
                    break;
            }
        }

        private void ApplyFaceState()
        {
            if (_frontFace != null) _frontFace.SetActive(!_faceDown);
            if (_backFace  != null) _backFace.SetActive(_faceDown);
        }

        // --- Drag --------------------------------------------------------
        // uGUI splits click vs drag automatically via pixelDragThreshold.
        // Click input isn't wired here — the zone (or a higher driver)
        // adds its own handlers if needed.

        public void OnBeginDrag(PointerEventData e)
        {
            // Left button only — keep right click free for flip.
            if (e.button != PointerEventData.InputButton.Left) return;
            // Draggable and face-state are orthogonal — a face-down deck
            // card is still grabbable. Visibility is the zone's call,
            // mobility is the zone's call, neither depends on the other.
            if (!Draggable || _rootCanvas == null) return;
            _isDragging = true;
            _homeParent = transform.parent;
            _homeSiblingIndex = transform.GetSiblingIndex();
            // Remember the layout-relative position so anchor-back zones
            // without a LayoutGroup can restore the card's slot. For
            // LayoutGroup-driven zones this is overwritten on next layout
            // anyway, so capturing here is a no-op for them.
            _homeAnchoredPos = ((RectTransform)transform).anchoredPosition;

            transform.SetParent(_rootCanvas.transform, worldPositionStays: true);
            transform.SetAsLastSibling();
            _dragOffset = transform.position - (Vector3)e.position;

            // Stop blocking raycasts so a drop receiver underneath us can
            // catch the pointer event. Restored in OnEndDrag.
            if (_canvasGroup != null) _canvasGroup.blocksRaycasts = false;

            if (GhostAnchor && _homeParent != null)
            {
                // Drop a same-sized placeholder into the slot we vacated
                // so the LayoutGroup parent doesn't reflow.
                _ghost = new GameObject("DragGhost",
                    typeof(RectTransform), typeof(LayoutElement));
                _ghost.transform.SetParent(_homeParent, false);
                _ghost.transform.SetSiblingIndex(_homeSiblingIndex);
                var rect = ((RectTransform)transform).rect;
                var le = _ghost.GetComponent<LayoutElement>();
                le.preferredWidth = rect.width;
                le.preferredHeight = rect.height;
                le.minWidth = rect.width;
                le.minHeight = rect.height;
            }
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_isDragging) return;
            transform.position = (Vector3)e.position + _dragOffset;
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (!_isDragging) return;
            _isDragging = false;

            // Ghost goes first — destroy before reparenting so the card
            // doesn't slot in next to a stale placeholder.
            if (_ghost != null) { Destroy(_ghost); _ghost = null; }

            if (_canvasGroup != null) _canvasGroup.blocksRaycasts = true;

            // A drop receiver already took ownership — skip snap-back and
            // self-destruct; the destination zone will spawn a fresh view
            // from its own card prefab.
            if (Claimed)
            {
                _homeParent = null;
                Destroy(gameObject);
                return;
            }

            if (AnchorsBack && _homeParent != null)
            {
                transform.SetParent(_homeParent, worldPositionStays: false);
                transform.SetSiblingIndex(_homeSiblingIndex);
                // Restore the original layout-relative position. With a
                // LayoutGroup parent this gets overwritten on the next
                // layout pass; without one (Stack / Free) this is the only
                // thing that puts the card back where it belongs.
                ((RectTransform)transform).anchoredPosition = _homeAnchoredPos;
            }
            _homeParent = null;
            Dropped?.Invoke(this);
        }

        public void OnPointerClick(PointerEventData e)
        {
            // Right-click flips when the zone allows it. Left click is
            // intentionally unused at this layer; if you need click-select
            // later, branch here on InputButton.Left.
            if (e.button == PointerEventData.InputButton.Right && Flippable)
                Flip();
        }
    }
}
