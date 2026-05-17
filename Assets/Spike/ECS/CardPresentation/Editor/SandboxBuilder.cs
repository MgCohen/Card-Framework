#nullable enable
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Spike.CardPresentation.Editor
{
    /// <summary>
    /// One-shot builder — generates the CardView prefab, the four zone
    /// prefabs (Deck / Hand / Playfield / Graveyard), and the Sandbox.unity
    /// scene wired up with prefab instances + the
    /// <see cref="PresentationSandbox"/> driver. Safe to re-run; it
    /// overwrites existing assets.
    /// </summary>
    public static class SandboxBuilder
    {
        private const string PrefabFolder = "Assets/Spike/ECS/CardPresentation/Prefabs";
        private const string CardPrefabPath = PrefabFolder + "/CardView.prefab";
        private const string ScenePath = "Assets/Spike/ECS/CardPresentation/Sandbox.unity";

        [MenuItem("Tools/CardPresentation/Build Sandbox")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(PrefabFolder);

            var card = BuildCardPrefab();
            // HorizontalLayoutGroup is always added to the CardTrack;
            // CardZone.Awake enables/disables it based on the Layout enum.
            // Per-zone variant sizes: Deck/Graveyard narrow (stack), Hand
            // wide (spread row), Playfield medium (max 3 cards).
            var deck       = BuildZonePrefab<DeckZone>      ("DeckZone",       card, width: 200);
            var hand       = BuildZonePrefab<HandZone>      ("HandZone",       card, width: 1100);
            var playfield  = BuildZonePrefab<PlayfieldZone> ("PlayfieldZone",  card, width: 600);
            var graveyard  = BuildZonePrefab<GraveyardZone> ("GraveyardZone",  card, width: 200);

            BuildScene(deck, hand, playfield, graveyard);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("CardPresentation: built CardView prefab, 4 zone prefabs, and Sandbox.unity.");
        }

        // -----------------------------------------------------------------
        // Card prefab
        // -----------------------------------------------------------------

        private static GameObject BuildCardPrefab()
        {
            var root = new GameObject("CardView",
                typeof(RectTransform), typeof(Image), typeof(LayoutElement),
                typeof(CanvasGroup), typeof(UICardView));
            var rt = (RectTransform)root.transform;
            // Center anchors so PileZone's manual offset works from a known
            // origin; HLG-based zones drive position regardless.
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(140, 190);

            var rootImg = root.GetComponent<Image>();
            rootImg.color = new Color(0.18f, 0.20f, 0.26f);

            var le = root.GetComponent<LayoutElement>();
            le.preferredWidth = 140;
            le.preferredHeight = 190;
            le.minWidth = 140;
            le.minHeight = 190;

            // Front face — transparent container for the card's labels.
            var front = MakeChild("Front", root.transform, typeof(RectTransform), typeof(Image));
            Stretch((RectTransform)front.transform);
            var frontImg = front.GetComponent<Image>();
            frontImg.color = new Color(1, 1, 1, 0);
            frontImg.raycastTarget = false;

            // Name strip — starts after the cost gem so the gem doesn't crop the first letters.
            var nameText = MakeText("Name", front.transform, 12, TextAnchor.MiddleCenter, Color.white);
            var nameRt = (RectTransform)nameText.transform;
            nameRt.anchorMin = new Vector2(0, 1);
            nameRt.anchorMax = new Vector2(1, 1);
            nameRt.pivot = new Vector2(0.5f, 1);
            nameRt.offsetMin = new Vector2(38, -30);
            nameRt.offsetMax = new Vector2(-6, -4);

            // Cost gem — overlay at top-left.
            var costGem = MakeChild("CostGem", front.transform, typeof(RectTransform), typeof(Image));
            var costImg = costGem.GetComponent<Image>();
            costImg.color = new Color(0.15f, 0.30f, 0.55f);
            costImg.raycastTarget = false;
            var costRt = (RectTransform)costGem.transform;
            costRt.anchorMin = costRt.anchorMax = new Vector2(0, 1);
            costRt.pivot = new Vector2(0, 1);
            costRt.anchoredPosition = new Vector2(4, -4);
            costRt.sizeDelta = new Vector2(28, 28);
            var costText = MakeText("0", costGem.transform, 15, TextAnchor.MiddleCenter, Color.white);

            // Art panel — between name and stats.
            var artPanel = MakeChild("Art", front.transform, typeof(RectTransform), typeof(Image));
            var artPanelImg = artPanel.GetComponent<Image>();
            artPanelImg.color = new Color(0.10f, 0.10f, 0.14f);
            artPanelImg.raycastTarget = false;
            var artRt = (RectTransform)artPanel.transform;
            artRt.anchorMin = Vector2.zero;
            artRt.anchorMax = Vector2.one;
            artRt.offsetMin = new Vector2(6, 26);
            artRt.offsetMax = new Vector2(-6, -34);

            // Optional sprite (disabled when VM.Art is null).
            var artSprite = MakeChild("Sprite", artPanel.transform, typeof(RectTransform), typeof(Image));
            var artSpriteImg = artSprite.GetComponent<Image>();
            artSpriteImg.raycastTarget = false;
            artSpriteImg.enabled = false;
            Stretch((RectTransform)artSprite.transform);

            // Body text overlaid on art panel.
            var bodyText = MakeText("", artPanel.transform, 11, TextAnchor.MiddleCenter,
                new Color(0.92f, 0.92f, 0.88f));
            var bodyRt = (RectTransform)bodyText.transform;
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(5, 5);
            bodyRt.offsetMax = new Vector2(-5, -5);

            // Stats strip across the bottom.
            var statsText = MakeText("", front.transform, 14, TextAnchor.MiddleCenter, Color.white);
            var statsRt = (RectTransform)statsText.transform;
            statsRt.anchorMin = new Vector2(0, 0);
            statsRt.anchorMax = new Vector2(1, 0);
            statsRt.pivot = new Vector2(0.5f, 0);
            statsRt.offsetMin = new Vector2(4, 2);
            statsRt.offsetMax = new Vector2(-4, 22);

            // Back face.
            var back = MakeChild("Back", root.transform, typeof(RectTransform), typeof(Image));
            Stretch((RectTransform)back.transform);
            var backImg = back.GetComponent<Image>();
            backImg.color = new Color(0.30f, 0.10f, 0.10f);
            backImg.raycastTarget = false;
            var backMark = MakeText("✦", back.transform, 32, TextAnchor.MiddleCenter,
                new Color(0.85f, 0.75f, 0.30f));
            Stretch((RectTransform)backMark.transform);

            // Wire UICardView's [SerializeField] references via SerializedObject —
            // setting the prefab once at build-time so the user doesn't have
            // to wire them by hand in the inspector.
            var view = root.GetComponent<UICardView>();
            var canvasGroup = root.GetComponent<CanvasGroup>();
            var so = new SerializedObject(view);
            so.FindProperty("_root").objectReferenceValue        = rootImg;
            so.FindProperty("_frontFace").objectReferenceValue   = front;
            so.FindProperty("_backFace").objectReferenceValue    = back;
            so.FindProperty("_nameText").objectReferenceValue    = nameText;
            so.FindProperty("_costText").objectReferenceValue    = costText;
            so.FindProperty("_artPanel").objectReferenceValue    = artPanelImg;
            so.FindProperty("_artImage").objectReferenceValue    = artSpriteImg;
            so.FindProperty("_bodyText").objectReferenceValue    = bodyText;
            so.FindProperty("_statsText").objectReferenceValue   = statsText;
            so.FindProperty("_canvasGroup").objectReferenceValue = canvasGroup;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, CardPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // -----------------------------------------------------------------
        // Zone prefab
        // -----------------------------------------------------------------

        private static GameObject BuildZonePrefab<T>(string name, GameObject cardPrefab, float width = 800f)
            where T : CardZone
        {
            var path = $"{PrefabFolder}/{name}.prefab";

            // Zone root: VerticalLayoutGroup stacks Label above CardTrack.
            // No Image — the dark background lives on CardTrack so the
            // label visually floats above the slab.
            var go = new GameObject(name,
                typeof(RectTransform), typeof(LayoutElement), typeof(VerticalLayoutGroup), typeof(T));
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(width, 250);

            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = 250;
            le.minHeight = 250;

            var rootVlg = go.GetComponent<VerticalLayoutGroup>();
            rootVlg.spacing = 6;
            rootVlg.padding = new RectOffset(0, 0, 0, 0);
            rootVlg.childForceExpandWidth = true;
            rootVlg.childForceExpandHeight = false;
            rootVlg.childControlWidth = true;
            rootVlg.childControlHeight = true;
            rootVlg.childAlignment = TextAnchor.UpperLeft;

            // Label strip (transparent, just a Text).
            var labelGo = MakeChild("Label", go.transform,
                typeof(RectTransform), typeof(LayoutElement));
            labelGo.GetComponent<LayoutElement>().preferredHeight = 26;
            var labelText = MakeText("", labelGo.transform, 14, TextAnchor.MiddleLeft,
                new Color(0.78f, 0.80f, 0.95f));

            // CardTrack — dark slab, holds spawned cards. HLG zones get
            // the HorizontalLayoutGroup component; Pile leaves it absent
            // so manual positioning works.
            var trackGo = MakeChild("CardTrack", go.transform,
                typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            trackGo.GetComponent<LayoutElement>().preferredHeight = 218;
            var trackImg = trackGo.GetComponent<Image>();
            trackImg.color = new Color(0.06f, 0.07f, 0.10f, 0.70f);
            // raycastTarget = true: drops on the card area must hit
            // CardTrack so uGUI can bubble OnDrop up to CardZone. Cards
            // themselves disable raycasts on their own CanvasGroup while
            // dragging, so this doesn't shadow card clicks/drags.
            trackImg.raycastTarget = true;

            // Always add HLG — CardZone.Awake will disable it for
            // Stack / Free layouts. This keeps the prefab structure
            // identical across variants; the enum drives the behavior.
            var hlg = trackGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14;
            hlg.padding = new RectOffset(14, 14, 14, 14);
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childAlignment = TextAnchor.MiddleLeft;

            // Reset gets called when the component is freshly added to a
            // GameObject — so the zone defaults + label string are already
            // applied by the time we get here.

            var card = cardPrefab.GetComponent<UICardView>();
            var so = new SerializedObject(go.GetComponent<T>());
            so.FindProperty("_cardPrefab").objectReferenceValue    = card;
            so.FindProperty("_cardContainer").objectReferenceValue = trackGo.transform;
            so.FindProperty("_labelText").objectReferenceValue     = labelText;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        // -----------------------------------------------------------------
        // Scene
        // -----------------------------------------------------------------

        private static void BuildScene(GameObject deckP, GameObject handP,
                                       GameObject playfieldP, GameObject graveyardP)
        {
            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // EventSystem.
            new GameObject("EventSystem",
                typeof(EventSystem), typeof(InputSystemUIInputModule));

            // Canvas — ScreenSpaceOverlay, scaled to reference resolution.
            var canvasGo = new GameObject("Sandbox Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            // Table root — full-canvas dark backdrop. Zones are positioned
            // absolutely as children (no LayoutGroup); each zone's own
            // RectTransform anchored-position is what places it on screen.
            var tableGo = MakeChild("Table", canvasGo.transform,
                typeof(RectTransform), typeof(Image));
            Stretch((RectTransform)tableGo.transform);
            tableGo.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.08f, 1f);
            tableGo.GetComponent<Image>().raycastTarget = false;

            // Instantiate the four zone prefabs as children of Table. Each
            // stays a prefab instance — user can see them in the Hierarchy
            // and edit the source prefabs.
            //
            // Layout (centered on 1280x720):
            //   • Deck      —  left  of playfield, mid-row
            //   • Playfield — center row, top half
            //   • Graveyard — right of playfield, mid-row
            //   • Hand      — bottom band, full width
            var deckInst       = (GameObject)PrefabUtility.InstantiatePrefab(deckP,      tableGo.transform);
            var playfieldInst  = (GameObject)PrefabUtility.InstantiatePrefab(playfieldP, tableGo.transform);
            var graveyardInst  = (GameObject)PrefabUtility.InstantiatePrefab(graveyardP, tableGo.transform);
            var handInst       = (GameObject)PrefabUtility.InstantiatePrefab(handP,      tableGo.transform);

            PlaceZone(deckInst,      new Vector2(-460f,   60f));
            PlaceZone(playfieldInst, new Vector2(   0f,   60f));
            PlaceZone(graveyardInst, new Vector2( 460f,   60f));
            PlaceZone(handInst,      new Vector2(   0f, -235f));

            // Wire drag-canvas reference on each zone so cards know where
            // to reparent during drag.
            foreach (var zoneGo in new[] { deckInst, handInst, playfieldInst, graveyardInst })
            {
                var zone = zoneGo.GetComponent<CardZone>();
                var so = new SerializedObject(zone);
                so.FindProperty("_dragCanvas").objectReferenceValue = canvas;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // PresentationSandbox driver — wire references to the zone
            // instances we just placed.
            var driverGo = new GameObject("Sandbox", typeof(PresentationSandbox));
            var driver = driverGo.GetComponent<PresentationSandbox>();
            var ds = new SerializedObject(driver);
            ds.FindProperty("_deck").objectReferenceValue      = deckInst.GetComponent<DeckZone>();
            ds.FindProperty("_hand").objectReferenceValue      = handInst.GetComponent<HandZone>();
            ds.FindProperty("_playfield").objectReferenceValue = playfieldInst.GetComponent<PlayfieldZone>();
            ds.FindProperty("_graveyard").objectReferenceValue = graveyardInst.GetComponent<GraveyardZone>();
            ds.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        /// <summary>
        /// Position a zone prefab instance at an anchored position relative
        /// to canvas center. Preserves the prefab's own sizeDelta (each
        /// variant carries its own width: Hand wide, Deck/Graveyard narrow).
        /// </summary>
        private static void PlaceZone(GameObject zone, Vector2 anchoredPos)
        {
            var rt = (RectTransform)zone.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static GameObject MakeChild(string name, Transform parent, params System.Type[] components)
        {
            var go = new GameObject(name, components);
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Text MakeText(string content, Transform parent, int size,
                                     TextAnchor anchor, Color color)
        {
            var go = MakeChild("Text", parent, typeof(RectTransform), typeof(Text));
            Stretch((RectTransform)go.transform);
            var t = go.GetComponent<Text>();
            t.text = content;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        private static void Stretch(RectTransform rt, float padding = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padding, padding);
            rt.offsetMax = new Vector2(-padding, -padding);
        }
    }
}
