#nullable enable
using System;
using UnityEngine;

namespace TagAuthoringPrototype.Authoring
{
    /// <summary>
    /// Optional UI metadata block attached to a <see cref="TagDefinition"/>.
    /// The engine never reads this — it's a pure UI/presentation concern.
    /// UI code looks it up via <c>TagAuthoringRegistry.GetDefinition</c>
    /// and reads off the fields it needs.
    ///
    /// <para>Unity auto-instantiates the field on the parent
    /// <see cref="ScriptableObject"/>, so this is never null in practice;
    /// <see cref="IsEmpty"/> distinguishes "no presentation authored" from
    /// "designer filled it in." Tags without UI semantics simply leave the
    /// fields blank.</para>
    ///
    /// <para>This is a deliberate single inline block, not a polymorphic
    /// module list. If a second extension axis (analytics, audit, network
    /// policy) shows up later, the natural refactor is to migrate to a
    /// <c>[SerializeReference] List&lt;TagModule&gt;</c> on
    /// <see cref="TagDefinition"/> and make this the first module type.</para>
    /// </summary>
    [Serializable]
    public sealed class TagPresentation
    {
        [SerializeField] Sprite? icon;
        [SerializeField] string label = "";
        [SerializeField] string tooltip = "";
        [SerializeField] Color color = Color.white;

        public Sprite? Icon => icon;
        public string Label => label ?? "";
        public string Tooltip => tooltip ?? "";
        public Color Color => color;

        public bool IsEmpty
            => icon == null
            && string.IsNullOrEmpty(label)
            && string.IsNullOrEmpty(tooltip);

        public TagPresentation() { }

        /// <summary>Runtime-fallback ctor for the demo's no-asset path. Authoring code uses inspector + the parameterless ctor.</summary>
        internal TagPresentation(string label, Color color, string tooltip = "")
        {
            this.label = label;
            this.color = color;
            this.tooltip = tooltip;
        }
    }
}
