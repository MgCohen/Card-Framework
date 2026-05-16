#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Pipeline;
using VerticalSlice.Engine.Stream;
using VerticalSlice.Sample;

namespace VerticalSlice.UnityScene
{
    /// <summary>
    /// Minimal Unity-UI (legacy uGUI) play surface for the slice.
    /// Programmatically builds a Canvas + EventSystem + two mirrored
    /// player panels — hero stats / battlefield row / hand row — with
    /// clickable card buttons. Two-click targeting:
    /// <list type="number">
    /// <item>Click a spell card in hand → it highlights, valid targets
    /// (hero panels + minion buttons) tint green.</item>
    /// <item>Click a target → cast. Click the highlighted card again to
    /// cancel.</item>
    /// </list>
    ///
    /// <para>Decoupled from <see cref="SliceBootstrap"/> beyond a single
    /// <see cref="Bind"/> call — finds players via
    /// <c>store.With(Tags.IsPlayer)</c> and lays them out lowest-id-first
    /// at the top. Refresh is full-rebuild on every wire event; cheap at
    /// slice scale, dead simple to reason about.</para>
    ///
    /// <para>Cohabits with the IMGUI scrubber in <c>SliceBootstrap.OnGUI</c>
    /// — Canvas is anchored upper-left; IMGUI renders in its default
    /// stretchable column. If they overlap, drag the Game view wider or
    /// nudge the IMGUI area when we trim it.</para>
    /// </summary>
    public sealed class SliceUI : MonoBehaviour
    {
        private GameEngine _engine = null!;
        private Font _font = null!;

        // Layout roots — built once in BuildCanvas, populated each Refresh.
        private Canvas _canvas = null!;
        private Transform _topHandRow = null!;
        private Transform _topBfRow = null!;
        private Transform _botHandRow = null!;
        private Transform _botBfRow = null!;
        private Text _topHeroText = null!;
        private Text _botHeroText = null!;
        private Button _topHeroButton = null!;
        private Button _botHeroButton = null!;
        private Text _statusText = null!;

        // Targeting state. _pendingCard non-null = we're waiting for a
        // target click; entity buttons should tint to advertise themselves.
        private Entity? _pendingCard;
        private Entity? _pendingCaster;

        // Per-refresh map: entity-id → button. Lets us recolor in place
        // when targeting state changes without a full rebuild.
        private readonly Dictionary<int, Button> _entityButtons = new();

        public void Bind(GameEngine engine)
        {
            _engine = engine;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildCanvas();
            _engine.World.Log.OnAppend += OnWireEvent;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_engine != null) _engine.World.Log.OnAppend -= OnWireEvent;
            // Canvas is a separate GameObject created by Bind; clean it up
            // so re-binding (e.g. Reset) doesn't leave orphaned hierarchies.
            if (_canvas != null) Destroy(_canvas.gameObject);
        }

        private void OnWireEvent(WireEvent _) => Refresh();

        // --- Canvas construction --------------------------------------------

        private void BuildCanvas()
        {
            // EventSystem — required for UI input. Create if not present.
            // Project is on the new Input System package, so we use
            // InputSystemUIInputModule (StandaloneInputModule reads from
            // the legacy UnityEngine.Input class, which is disabled).
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem",
                    typeof(EventSystem),
                    typeof(InputSystemUIInputModule));

            var canvasGo = new GameObject("SliceUI Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            // Single vertical layout — top hero / top battlefield / top hand
            // / center status / bottom hand / bottom battlefield / bottom hero.
            var root = Make("Root", _canvas.transform,
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            var rootRect = (RectTransform)root.transform;
            // Anchor to the right two-thirds so the IMGUI scrubber on the
            // left doesn't get covered.
            rootRect.anchorMin = new Vector2(0.30f, 0f);
            rootRect.anchorMax = new Vector2(1.0f,  1f);
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            var vlg = root.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6;
            vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;

            // Top player section.
            (_topHeroText, _topHeroButton) = MakeHeroPanel(root.transform, "TopHero");
            _topHeroButton.onClick.AddListener(() => OnEntityClicked(_topHeroButton, /*hero=*/true));
            _topBfRow   = MakeRow(root.transform, "TopBattlefield");
            _topHandRow = MakeRow(root.transform, "TopHand");

            // Status / separator strip.
            var statusGo = Make("Status", root.transform,
                typeof(RectTransform), typeof(LayoutElement));
            statusGo.GetComponent<LayoutElement>().preferredHeight = 28;
            _statusText = MakeText("", statusGo.transform, 14,
                TextAnchor.MiddleCenter, new Color(0.85f, 0.85f, 0.55f))
                .GetComponent<Text>();

            // Bottom player section (mirrored — hand first so it visually
            // sits "in front of" the battlefield from the bottom player's
            // perspective).
            _botHandRow = MakeRow(root.transform, "BotHand");
            _botBfRow   = MakeRow(root.transform, "BotBattlefield");
            (_botHeroText, _botHeroButton) = MakeHeroPanel(root.transform, "BotHero");
            _botHeroButton.onClick.AddListener(() => OnEntityClicked(_botHeroButton, /*hero=*/true));
        }

        private (Text text, Button button) MakeHeroPanel(Transform parent, string name)
        {
            var go = Make(name, parent,
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.25f);
            go.GetComponent<LayoutElement>().preferredHeight = 56;

            var label = MakeText("Hero", go.transform, 18, TextAnchor.MiddleLeft, Color.white);
            var labelRect = (RectTransform)label.transform;
            labelRect.offsetMin = new Vector2(16, 0);
            labelRect.offsetMax = new Vector2(-16, 0);
            return (label.GetComponent<Text>(), go.GetComponent<Button>());
        }

        private Transform MakeRow(Transform parent, string name)
        {
            var go = Make(name, parent,
                typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.GetComponent<LayoutElement>().preferredHeight = 76;
            var hlg = go.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 6;
            hlg.padding = new RectOffset(4, 4, 4, 4);
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            return go.transform;
        }

        // --- Refresh ---------------------------------------------------------

        private void Refresh()
        {
            _entityButtons.Clear();

            // Find both players, lowest-id-first → top. The slice has
            // exactly two; defensive fallback if not.
            var players = new List<Entity>();
            foreach (var e in _engine.EntityStore.With(Tags.IsPlayer))
            {
                var ent = _engine.EntityStore.Get(e.Id);
                if (ent != null) players.Add(ent);
            }
            players.Sort((a, b) => a.Id.CompareTo(b.Id));

            Entity? top = players.Count > 0 ? players[0] : null;
            Entity? bot = players.Count > 1 ? players[1] : null;

            RenderPlayer(top, _topHeroText, _topHeroButton, _topBfRow, _topHandRow);
            RenderPlayer(bot, _botHeroText, _botHeroButton, _botBfRow, _botHandRow);

            UpdateStatusText();
            ApplyTargetTinting();
        }

        private void RenderPlayer(Entity? player, Text heroText, Button heroButton,
                                  Transform bfRow, Transform handRow)
        {
            ClearChildren(bfRow);
            ClearChildren(handRow);

            if (player == null)
            {
                heroText.text = "—";
                heroButton.gameObject.SetActive(false);
                return;
            }
            heroButton.gameObject.SetActive(true);
            heroText.text = $"{player.PlayerName()}    HP {player.Health()}/{player.MaxHealth()}    Armor {player.Armor()}";
            _entityButtons[player.Id] = heroButton;

            // Each player's zones are owner-tagged; battlefield + hand
            // come straight off the indexed store.
            foreach (var zone in _engine.EntityStore.With(Tags.IsZone).And(Tags.Owner, player.Id))
            {
                if (zone.IsBattlefield())
                {
                    foreach (var card in _engine.EntityStore.CardsIn(zone))
                        AddCardButton(bfRow, (Entity)card, isHand: false);
                }
                else if (!zone.IsGraveyard())   // hand isn't marker-tagged; falls through here
                {
                    foreach (var card in _engine.EntityStore.CardsIn(zone))
                        AddCardButton(handRow, (Entity)card, isHand: true);
                }
            }
        }

        private void AddCardButton(Transform row, Entity card, bool isHand)
        {
            var go = Make($"Card#{card.Id}", row,
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.GetComponent<LayoutElement>().preferredWidth = isHand ? 130 : 110;
            go.GetComponent<LayoutElement>().preferredHeight = isHand ? 64 : 64;
            go.GetComponent<Image>().color = isHand
                ? new Color(0.22f, 0.28f, 0.42f)   // hand cards — blueish
                : new Color(0.24f, 0.38f, 0.24f);  // minions — greenish

            var labelText = BuildCardLabel(card, isHand);
            MakeText(labelText, go.transform, 12, TextAnchor.MiddleCenter, Color.white);

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => OnEntityClicked(btn, hero: false, cardOverride: card, inHand: isHand));
            _entityButtons[card.Id] = btn;
        }

        private string BuildCardLabel(Entity card, bool isHand)
        {
            var name = card.CardName();
            if (isHand)
            {
                var cost = card.Cost();
                return $"[{cost}] {name}";
            }
            // Minion on battlefield.
            var atk = card.Attack();
            var hp = card.Health();
            var ds = card.DivineShield() ? " ◆" : "";
            var bl = card.Backlash() ? " ↩" : "";
            var dr = _engine.EffectStore.HasDirect<Deathrattle>(card) ? " ☠" : "";
            return $"{name}\n{atk}/{hp}{ds}{bl}{dr}";
        }

        // --- Click handling --------------------------------------------------

        private void OnEntityClicked(Button btn, bool hero,
                                     Entity? cardOverride = null, bool inHand = false)
        {
            // Find entity id by reverse-looking up the button. Cheap dict
            // scan; entity count is small.
            int? clickedId = null;
            foreach (var kv in _entityButtons)
            {
                if (kv.Value == btn) { clickedId = kv.Key; break; }
            }
            if (clickedId == null) return;
            var clicked = _engine.EntityStore.Get(clickedId.Value);
            if (clicked == null) return;

            // Case 1: nothing pending. Only spell cards in hand start the
            // targeting flow.
            if (_pendingCard == null)
            {
                if (cardOverride != null && inHand && cardOverride.IsSpell())
                {
                    _pendingCard = cardOverride;
                    _pendingCaster = _engine.EntityStore.Get(cardOverride.OwnerId());
                    UpdateStatusText();
                    ApplyTargetTinting();
                }
                return;
            }

            // Case 2: clicking the pending card again — cancel.
            if (cardOverride != null && cardOverride.Id == _pendingCard.Id)
            {
                CancelTargeting();
                return;
            }

            // Case 3: a target was clicked. Spells target anything that's
            // a player (hero) or a minion. Tighter rules can land later.
            if (IsValidTarget(clicked))
            {
                if (_pendingCaster != null)
                    _engine.Enqueue(new PlayCard(_pendingCard, _pendingCaster, clicked));
                CancelTargeting();
                return;
            }

            // Anything else — silent no-op. Keep targeting active.
        }

        private bool IsValidTarget(Entity e) => e.IsPlayer() || e.IsMinion();

        private void CancelTargeting()
        {
            _pendingCard = null;
            _pendingCaster = null;
            UpdateStatusText();
            ApplyTargetTinting();
        }

        // --- Visuals ---------------------------------------------------------

        private void UpdateStatusText()
        {
            if (_pendingCard == null)
            {
                _statusText.text = "click a card to begin";
                return;
            }
            _statusText.text = $"{_pendingCaster?.PlayerName()} → {_pendingCard.CardName()}  —  pick a target";
        }

        private void ApplyTargetTinting()
        {
            foreach (var kv in _entityButtons)
            {
                var ent = _engine.EntityStore.Get(kv.Key);
                if (ent == null) continue;
                var img = kv.Value.GetComponent<Image>();
                Color tint = img.color;
                // Reset to base (re-fetch from how we built it).
                if (ent.IsPlayer())
                    tint = new Color(0.18f, 0.18f, 0.25f);
                else if (ent.IsMinion())
                    tint = new Color(0.24f, 0.38f, 0.24f);
                else if (ent.IsSpell())
                    tint = new Color(0.22f, 0.28f, 0.42f);

                if (_pendingCard != null)
                {
                    if (ent.Id == _pendingCard.Id)
                        tint = new Color(0.65f, 0.55f, 0.20f);            // pending card — amber
                    else if (IsValidTarget(ent))
                        tint = Color.Lerp(tint, new Color(0.30f, 0.65f, 0.30f), 0.55f);  // target — green tint
                }
                img.color = tint;
            }
        }

        // --- Helpers ---------------------------------------------------------

        private static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                GameObject.Destroy(t.GetChild(i).gameObject);
        }

        private static GameObject Make(string name, Transform parent, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            go.transform.SetParent(parent, false);
            return go;
        }

        private GameObject MakeText(string content, Transform parent, int size,
                                    TextAnchor anchor, Color color)
        {
            var go = Make("Text", parent, typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<Text>();
            t.text = content;
            t.font = _font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.raycastTarget = false;   // text shouldn't block button clicks
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return go;
        }
    }
}
