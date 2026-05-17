#nullable enable
using UnityEngine;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Tags;
using TagAuthoringPrototype.Authoring;

namespace TagAuthoringPrototype.Demo
{
    /// <summary>
    /// Demonstrates Pattern C (data-defined tags) coexisting with Pattern A
    /// (code-defined tags) on the same <see cref="Entity"/>. Drop on any
    /// GameObject and Play.
    ///
    /// <para>The serialized fields hold data-defined <see cref="TagDefinition"/>
    /// assets. Wire them in the inspector — or run the
    /// <c>Tools/TagAuthoringPrototype/Build Sample Scene</c> menu, which
    /// authors a set of sample assets and wires them automatically.</para>
    ///
    /// <para>If a field is left null, an in-memory placeholder is created
    /// at Awake so the scene is runnable out of the box. The placeholder
    /// hides the authoring story (no <c>.asset</c> file on disk) but the
    /// runtime shape is identical — <c>ScriptableObject.CreateInstance</c>
    /// produces a fully-functional <see cref="TagDefinition"/>.</para>
    /// </summary>
    public sealed class TagAuthoringDemo : MonoBehaviour
    {
        [Header("Data-defined tags — designer-authored SO assets (Pattern C)")]
        [Tooltip("Leave null to fall back to an in-memory placeholder at Awake.")]
        [SerializeField] IntTag?    healthTag;
        [SerializeField] IntTag?    manaTag;
        [SerializeField] BoolTag?   onFireTag;
        [SerializeField] StringTag? elementTag;

        Entity _entity = null!;

        // Pattern A — code-defined tag right here in the prototype. Slot 500
        // is free in the slice's slot space. Demonstrates code-defined and
        // data-defined coexisting in the same TagRegistry.
        static readonly TagKey<int> DemoLevel = new(500, "demoLevel");

        void Awake()
        {
            EnsureTagsExist();

            TagAuthoringRegistry.Bake(new TagDefinition[]
            {
                healthTag!, manaTag!, onFireTag!, elementTag!
            });

            _entity = new Entity();

            // Initial values — the implicit conversion `TagDefinition<T> → TagKey<T>`
            // means call sites read identically to code-defined tags.
            _entity.Set(healthTag!,  30);
            _entity.Set(manaTag!,    10);
            _entity.Set(onFireTag!,  false);
            _entity.Set(elementTag!, "Fire");
            _entity.Set(DemoLevel,   1);
        }

        // --- Fallback: build in-memory placeholders for unwired fields ----

        void EnsureTagsExist()
        {
            healthTag  ??= MakeRuntime<IntTag>(   1001, "health",  "Health",   new Color(0.85f, 0.20f, 0.20f));
            manaTag    ??= MakeRuntime<IntTag>(   1002, "mana",    "Mana",     new Color(0.20f, 0.45f, 0.85f));
            onFireTag  ??= MakeRuntime<BoolTag>(  1003, "onFire",  "On Fire",  new Color(0.95f, 0.50f, 0.10f));
            elementTag ??= MakeRuntime<StringTag>(1004, "element", "Element",  new Color(0.65f, 0.65f, 0.65f));
        }

        static T MakeRuntime<T>(int slot, string id, string label, Color color) where T : TagDefinition
        {
            var inst = ScriptableObject.CreateInstance<T>();
            inst.InitRuntime(id, slot, new TagPresentation(label, color));
            inst.name = $"{typeof(T).Name}_{id}";
            inst.hideFlags = HideFlags.HideAndDontSave;
            return inst;
        }

        // --- IMGUI panel --------------------------------------------------

        void OnGUI()
        {
            const int panelWidth = 520;
            GUILayout.BeginArea(new Rect(20, 20, panelWidth, Screen.height - 40), GUI.skin.box);
            GUILayout.Label("Tag Authoring Prototype — Pattern A + Pattern C", BoldLabel);
            GUILayout.Space(6);

            GUILayout.Label("Pattern A — code-defined", SubLabel);
            DrawCodeRow("demoLevel (slot 500)", _entity.Get(DemoLevel),
                () => _entity.Set(DemoLevel, _entity.Get(DemoLevel) + 1),
                () => _entity.Set(DemoLevel, _entity.Get(DemoLevel) - 1));

            GUILayout.Space(10);
            GUILayout.Label("Pattern C — data-defined", SubLabel);

            DrawIntRow(healthTag!,  +5, -5);
            DrawIntRow(manaTag!,    +1, -1);
            DrawBoolRow(onFireTag!);
            DrawStringRow(elementTag!, "Fire", "Water", "Earth", "Air");

            GUILayout.Space(10);
            GUILayout.Label($"Registered data-defined tags: {TagAuthoringRegistry.Count}", SubLabel);
            GUILayout.Label("(Same TagRegistry as Pattern A — slots in one shared int space.)", SubLabel);
            GUILayout.EndArea();
        }

        // --- Row drawers --------------------------------------------------

        void DrawCodeRow(string label, int value, System.Action onPlus, System.Action onMinus)
        {
            GUILayout.BeginHorizontal();
            DrawColorSwatch(Color.gray);
            GUILayout.Label(label, GUILayout.Width(240));
            GUILayout.Label(value.ToString(), GUILayout.Width(60));
            if (GUILayout.Button("+", GUILayout.Width(28))) onPlus();
            if (GUILayout.Button("-", GUILayout.Width(28))) onMinus();
            GUILayout.EndHorizontal();
        }

        void DrawIntRow(IntTag tag, int plusDelta, int minusDelta)
        {
            var pres = TagAuthoringRegistry.GetDefinition(tag)?.Presentation;
            int value = _entity.Get(tag);
            GUILayout.BeginHorizontal();
            DrawColorSwatch(pres?.Color ?? Color.white);
            GUILayout.Label(LabelFor(tag, pres), GUILayout.Width(240));
            GUILayout.Label(value.ToString(), GUILayout.Width(60));
            if (GUILayout.Button($"+{plusDelta}", GUILayout.Width(40))) _entity.Set(tag, value + plusDelta);
            if (GUILayout.Button($"{minusDelta}", GUILayout.Width(40))) _entity.Set(tag, value + minusDelta);
            GUILayout.EndHorizontal();
        }

        void DrawBoolRow(BoolTag tag)
        {
            var pres = TagAuthoringRegistry.GetDefinition(tag)?.Presentation;
            bool value = _entity.Get(tag);
            GUILayout.BeginHorizontal();
            DrawColorSwatch(pres?.Color ?? Color.white);
            GUILayout.Label(LabelFor(tag, pres), GUILayout.Width(240));
            GUILayout.Label(value ? "true" : "false", GUILayout.Width(60));
            if (GUILayout.Button("Toggle", GUILayout.Width(80))) _entity.Set(tag, !value);
            GUILayout.EndHorizontal();
        }

        void DrawStringRow(StringTag tag, params string[] options)
        {
            var pres = TagAuthoringRegistry.GetDefinition(tag)?.Presentation;
            string value = _entity.Get(tag) ?? "";
            GUILayout.BeginHorizontal();
            DrawColorSwatch(pres?.Color ?? Color.white);
            GUILayout.Label(LabelFor(tag, pres), GUILayout.Width(240));
            GUILayout.Label(value, GUILayout.Width(60));
            foreach (var opt in options)
                if (GUILayout.Button(opt, GUILayout.Width(56))) _entity.Set(tag, opt);
            GUILayout.EndHorizontal();
        }

        static string LabelFor(TagDefinition def, TagPresentation? pres)
        {
            string display = !string.IsNullOrEmpty(pres?.Label) ? pres!.Label : def.Id;
            return $"{display}  ({def.Id} / slot {def.Slot})";
        }

        static void DrawColorSwatch(Color color)
        {
            var rect = GUILayoutUtility.GetRect(14, 14, GUILayout.Width(14), GUILayout.Height(14));
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        static GUIStyle? _boldLabel;
        static GUIStyle BoldLabel => _boldLabel ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
        static GUIStyle? _subLabel;
        static GUIStyle SubLabel => _subLabel ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Italic };
    }
}
