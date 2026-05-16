#nullable enable
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using VerticalSlice.Engine.Pipeline;
using VerticalSlice.Engine.Stream;
using VerticalSlice.Mirror;
using VerticalSlice.Sample;

namespace VerticalSlice.UnityScene
{
    /// <summary>
    /// Minimal Unity-UI (legacy uGUI) play surface for the slice.
    /// Programmatically builds a Canvas + EventSystem + two mirrored
    /// player rows — hero stats / graveyard count / battlefield / hand —
    /// with clickable card buttons. Two-click targeting:
    /// <list type="number">
    /// <item>Click a spell card in hand → it tints amber, valid targets
    /// (hero panels + minion buttons) tint green.</item>
    /// <item>Click a target → cast. Click the highlighted card again to
    /// cancel.</item>
    /// </list>
    ///
    /// <para><b>Renders from <see cref="MirrorWorld"/>, not engine state.</b>
    /// Mirror = engine state at the scrub cursor; when at head it equals
    /// live, when rewound it shows the past. Cast actions advance the
    /// cursor to the new head; SliceBootstrap's scrub slider can rewind it.
    /// When rewound, buttons disable so the past stays read-only.</para>
    /// </summary>
    public sealed class SliceUI : MonoBehaviour
    {
        private GameEngine _engine = null!;
        private MirrorWorld _mirror = null!;
        private ScrubController _scrub = null!;
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
        private Text _topGraveText = null!;
        private Text _botGraveText = null!;
        private Text _statusText = null!;

        // Targeting state. _pendingCardId non-null = waiting for a target.
        private int? _pendingCardId;
        private int? _pendingCasterId;

        // Per-refresh map: entity-id → button. Lets us recolor in place
        // when targeting state changes without a full rebuild.
        private readonly Dictionary<int, Button> _entityButtons = new();

        // Cached per-refresh kind — minion / spell / hero / other — used by
        // tinting and target-validity checks. Mirror entities don't have
        // typed accessors so we lift these once on render.
        private readonly Dictionary<int, EntityKind> _entityKinds = new();

        private enum EntityKind { Hero, Minion, SpellInHand, Other }

        public void Bind(GameEngine engine, MirrorWorld mirror, ScrubController scrub)
        {
            _engine = engine;
            _mirror = mirror;
            _scrub = scrub;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildCanvas();
            Refresh();
        }

        /// <summary>
        /// Called by <see cref="SliceBootstrap"/> when the scrub slider
        /// moves. Advances the mirror to the requested cursor and rebuilds
        /// the UI from there. Buttons go non-interactive when the cursor
        /// is below head — past state stays read-only.
        /// </summary>
        public void SetCursor(int cursor)
        {
            _scrub.GoTo(cursor);
            // Rewinding while a card is mid-targeting would dangle —
            // cancel any pending action so the user isn't selecting against
            // a stale snapshot.
            _pendingCardId = null;
            _pendingCasterId = null;
            Refresh();
        }

        // --- Canvas construction --------------------------------------------

        private void BuildCanvas()
        {
            // EventSystem — required for UI input. Project is on the new
            // Input System package, so we use InputSystemUIInputModule.
            if (FindAnyObjectByType<EventSystem>() == null)
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

            var root = Make("Root", _canvas.transform,
                typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            var rootRect = (RectTransform)root.transform;
            // Anchor to the right two-thirds so the IMGUI debug strip on the
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

            // Top player.
            var topHero = MakeHeroRow(root.transform, "TopHero");
            _topHeroText   = topHero.heroText;
            _topHeroButton = topHero.heroButton;
            _topGraveText  = topHero.graveText;
            _topHeroButton.onClick.AddListener(() => OnEntityClicked(_topHeroButton));

            _topBfRow   = MakeRow(root.transform, "TopBattlefield");
            _topHandRow = MakeRow(root.transform, "TopHand");

            // Status / separator strip.
            var statusGo = Make("Status", root.transform,
                typeof(RectTransform), typeof(LayoutElement));
            statusGo.GetComponent<LayoutElement>().preferredHeight = 28;
            _statusText = MakeText("", statusGo.transform, 14,
                TextAnchor.MiddleCenter, new Color(0.85f, 0.85f, 0.55f))
                .GetComponent<Text>();

            // Bottom player (mirrored).
            _botHandRow = MakeRow(root.transform, "BotHand");
            _botBfRow   = MakeRow(root.transform, "BotBattlefield");

            var botHero = MakeHeroRow(root.transform, "BotHero");
            _botHeroText   = botHero.heroText;
            _botHeroButton = botHero.heroButton;
            _botGraveText  = botHero.graveText;
            _botHeroButton.onClick.AddListener(() => OnEntityClicked(_botHeroButton));
        }

        private (Text heroText, Button heroButton, Text graveText) MakeHeroRow(Transform parent, string name)
        {
            var rowGo = Make(name, parent,
                typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowGo.GetComponent<LayoutElement>().preferredHeight = 56;
            var hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            // Hero panel — wide, clickable.
            var heroGo = Make("HeroPanel", rowGo.transform,
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            heroGo.GetComponent<Image>().color = new Color(0.18f, 0.18f, 0.25f);
            var heroLE = heroGo.GetComponent<LayoutElement>();
            heroLE.flexibleWidth = 1;
            heroLE.preferredHeight = 56;
            var heroLabel = MakeText("Hero", heroGo.transform, 18,
                TextAnchor.MiddleLeft, Color.white);
            var heroLabelRect = (RectTransform)heroLabel.transform;
            heroLabelRect.offsetMin = new Vector2(16, 0);
            heroLabelRect.offsetMax = new Vector2(-16, 0);

            // Graveyard count panel — narrow, display-only.
            var graveGo = Make("GraveyardPanel", rowGo.transform,
                typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            graveGo.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.14f);
            var graveLE = graveGo.GetComponent<LayoutElement>();
            graveLE.preferredWidth = 110;
            graveLE.preferredHeight = 56;
            var graveLabel = MakeText("☠ 0", graveGo.transform, 14,
                TextAnchor.MiddleCenter, new Color(0.75f, 0.75f, 0.75f));

            return (heroLabel.GetComponent<Text>(),
                    heroGo.GetComponent<Button>(),
                    graveLabel.GetComponent<Text>());
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

        // --- Refresh (renders from mirror) ----------------------------------

        private void Refresh()
        {
            _entityButtons.Clear();
            _entityKinds.Clear();

            // Find both players in mirror, lowest-id first → top.
            var players = new List<MirrorEntity>();
            foreach (var kv in _mirror.Entities)
                if (MBool(kv.Value, "isPlayer")) players.Add(kv.Value);
            players.Sort((a, b) => a.Id.CompareTo(b.Id));

            MirrorEntity? top = players.Count > 0 ? players[0] : null;
            MirrorEntity? bot = players.Count > 1 ? players[1] : null;

            RenderPlayer(top, _topHeroText, _topHeroButton, _topGraveText, _topBfRow, _topHandRow);
            RenderPlayer(bot, _botHeroText, _botHeroButton, _botGraveText, _botBfRow, _botHandRow);

            UpdateStatusText();
            ApplyTargetTinting();
            ApplyInteractability();
        }

        private void RenderPlayer(MirrorEntity? player,
                                  Text heroText, Button heroButton, Text graveText,
                                  Transform bfRow, Transform handRow)
        {
            ClearChildren(bfRow);
            ClearChildren(handRow);

            if (player == null)
            {
                heroText.text = "—";
                graveText.text = "";
                heroButton.gameObject.SetActive(false);
                return;
            }
            heroButton.gameObject.SetActive(true);
            heroText.text = $"{MStr(player, "playerName")}    HP {MInt(player, "health")}/{MInt(player, "maxHealth")}    Armor {MInt(player, "armor")}";
            _entityButtons[player.Id] = heroButton;
            _entityKinds[player.Id] = EntityKind.Hero;

            // Find this player's zones in mirror. The hand isn't marker-tagged
            // (it's just IsZone with no battlefield/graveyard sub-tag).
            MirrorEntity? handZone = null;
            MirrorEntity? bfZone = null;
            MirrorEntity? graveZone = null;
            foreach (var kv in _mirror.Entities)
            {
                var z = kv.Value;
                if (!MBool(z, "isZone")) continue;
                if (MInt(z, "owner") != player.Id) continue;
                if (MBool(z, "isBattlefield")) bfZone = z;
                else if (MBool(z, "isGraveyard")) graveZone = z;
                else handZone = z;
            }

            // Battlefield + hand are populated.
            if (bfZone != null) PopulateZone(bfRow, bfZone, isHand: false);
            if (handZone != null) PopulateZone(handRow, handZone, isHand: true);

            // Graveyard is just a count for now.
            int graveCount = 0;
            if (graveZone != null)
                foreach (var kv in _mirror.Entities)
                    if (MInt(kv.Value, "zone") == graveZone.Id) graveCount++;
            graveText.text = $"☠ {graveCount}";
        }

        private void PopulateZone(Transform row, MirrorEntity zone, bool isHand)
        {
            // Walk mirror entities, pick cards whose Zone tag points at us.
            // Slice scale → linear scan is fine.
            foreach (var kv in _mirror.Entities)
            {
                var c = kv.Value;
                if (!MBool(c, "isCard")) continue;
                if (MInt(c, "zone") != zone.Id) continue;
                AddCardButton(row, c, isHand);
            }
        }

        private void AddCardButton(Transform row, MirrorEntity card, bool isHand)
        {
            var go = Make($"Card#{card.Id}", row,
                typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = isHand ? 130 : 110;
            le.preferredHeight = 64;
            var img = go.GetComponent<Image>();
            img.color = isHand
                ? new Color(0.22f, 0.28f, 0.42f)   // hand cards — blueish
                : new Color(0.24f, 0.38f, 0.24f);  // minions — greenish

            MakeText(BuildCardLabel(card, isHand), go.transform, 12,
                     TextAnchor.MiddleCenter, Color.white);

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => OnEntityClicked(btn));
            _entityButtons[card.Id] = btn;
            _entityKinds[card.Id] = isHand && MBool(card, "isSpell")
                ? EntityKind.SpellInHand
                : MBool(card, "isMinion") ? EntityKind.Minion : EntityKind.Other;
        }

        private string BuildCardLabel(MirrorEntity card, bool isHand)
        {
            var name = MStr(card, "cardName");
            if (isHand)
                return $"[{MInt(card, "cost")}] {name}";

            // Minion on battlefield.
            var atk = MInt(card, "attack");
            var hp = MInt(card, "health");
            var ds = MBool(card, "divineShield") ? " ◆" : "";
            var bl = MBool(card, "backlash") ? " ↩" : "";
            var dr = MBool(card, "Deathrattle") ? " ☠" : "";
            return $"{name}\n{atk}/{hp}{ds}{bl}{dr}";
        }

        // --- Click handling --------------------------------------------------

        private void OnEntityClicked(Button btn)
        {
            // Reverse-lookup id from button. Cheap; entity count is small.
            int? clickedId = null;
            foreach (var kv in _entityButtons)
            {
                if (kv.Value == btn) { clickedId = kv.Key; break; }
            }
            if (clickedId == null) return;
            if (!_entityKinds.TryGetValue(clickedId.Value, out var kind)) return;

            // Case 1: nothing pending. Only spell cards in hand start the
            // targeting flow.
            if (_pendingCardId == null)
            {
                if (kind != EntityKind.SpellInHand) return;
                var card = _engine.EntityStore.Get(clickedId.Value);
                if (card == null) return;
                _pendingCardId = clickedId.Value;
                _pendingCasterId = card.OwnerId();
                UpdateStatusText();
                ApplyTargetTinting();
                return;
            }

            // Case 2: clicking the pending card again — cancel.
            if (clickedId.Value == _pendingCardId.Value)
            {
                CancelTargeting();
                return;
            }

            // Case 3: a target was clicked. Spells target heroes and
            // minions. Tighter rules can land later.
            if (kind == EntityKind.Hero || kind == EntityKind.Minion)
            {
                var card = _engine.EntityStore.Get(_pendingCardId.Value);
                var caster = _pendingCasterId.HasValue
                    ? _engine.EntityStore.Get(_pendingCasterId.Value) : null;
                var target = _engine.EntityStore.Get(clickedId.Value);
                if (card != null && caster != null && target != null)
                    _engine.Enqueue(new PlayCard(card, caster, target));
                CancelTargeting();
                // Catch the mirror up to the new head — Enqueue may have
                // produced many events — and refresh.
                _scrub.GoTo(_engine.World.Log.Count);
                Refresh();
                return;
            }

            // Anything else — silent no-op. Targeting stays active.
        }

        private void CancelTargeting()
        {
            _pendingCardId = null;
            _pendingCasterId = null;
            UpdateStatusText();
            ApplyTargetTinting();
        }

        // --- Visuals ---------------------------------------------------------

        private void UpdateStatusText()
        {
            if (!IsAtHead())
            {
                _statusText.text = $"scrubbed back ({_scrub.Cursor}/{_engine.World.Log.Count}) — read-only";
                return;
            }
            if (_pendingCardId == null)
            {
                _statusText.text = "click a card to begin";
                return;
            }
            var card = _engine.EntityStore.Get(_pendingCardId.Value);
            var caster = _pendingCasterId.HasValue
                ? _engine.EntityStore.Get(_pendingCasterId.Value) : null;
            var casterName = caster != null ? caster.PlayerName() : "?";
            var cardName = card != null ? card.CardName() : "?";
            _statusText.text = $"{casterName} → {cardName}  —  pick a target";
        }

        private void ApplyTargetTinting()
        {
            foreach (var kv in _entityButtons)
            {
                var img = kv.Value.GetComponent<Image>();
                if (img == null) continue;
                var kind = _entityKinds.TryGetValue(kv.Key, out var k) ? k : EntityKind.Other;
                Color tint = kind switch
                {
                    EntityKind.Hero        => new Color(0.18f, 0.18f, 0.25f),
                    EntityKind.Minion      => new Color(0.24f, 0.38f, 0.24f),
                    EntityKind.SpellInHand => new Color(0.22f, 0.28f, 0.42f),
                    _                      => new Color(0.22f, 0.22f, 0.22f),
                };
                if (_pendingCardId.HasValue && IsAtHead())
                {
                    if (kv.Key == _pendingCardId.Value)
                        tint = new Color(0.65f, 0.55f, 0.20f);   // pending — amber
                    else if (kind == EntityKind.Hero || kind == EntityKind.Minion)
                        tint = Color.Lerp(tint, new Color(0.30f, 0.65f, 0.30f), 0.55f);
                }
                img.color = tint;
            }
        }

        private void ApplyInteractability()
        {
            bool live = IsAtHead();
            foreach (var kv in _entityButtons)
                kv.Value.interactable = live;
        }

        private bool IsAtHead() => _scrub.Cursor >= _engine.World.Log.Count;

        // --- Helpers ---------------------------------------------------------

        // MirrorEntity tags are a string-keyed dictionary; these typed
        // wrappers keep the rest of the file readable. Tag string keys
        // come from TagKey.Name (the wire label declared in Tags.cs and
        // the Pattern B class name for synthesized keys).

        private static bool MBool(MirrorEntity e, string key)
            => e.Tags.TryGetValue(key, out var v) && v is bool b && b;

        private static int MInt(MirrorEntity e, string key)
            => e.Tags.TryGetValue(key, out var v) && v is int i ? i : 0;

        private static string MStr(MirrorEntity e, string key)
            => e.Tags.TryGetValue(key, out var v) && v is string s ? s : "";

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

        private void OnDestroy()
        {
            // Canvas is a separate GameObject created by Bind; clean it up
            // so re-binding (e.g. Reset) doesn't leave orphaned hierarchies.
            if (_canvas != null) Destroy(_canvas.gameObject);
        }
    }
}
