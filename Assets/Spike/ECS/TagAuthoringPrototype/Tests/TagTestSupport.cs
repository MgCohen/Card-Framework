#nullable enable
using System.Reflection;
using System.Threading;
using UnityEngine;
using TagAuthoringPrototype.Authoring;

namespace TagAuthoringPrototype.Tests
{
    /// <summary>
    /// Shared test helpers. Two reasons this exists:
    ///
    /// <para><b>Slot uniqueness.</b> <c>TagRegistry</c> is process-global and
    /// has no test-reset hook — slots claimed in one test stay claimed for
    /// the whole Unity domain. Tests allocate slots from a monotonic counter
    /// in a high range (well above any code-defined Pattern A / Pattern B
    /// tags) so no two tests collide.</para>
    ///
    /// <para><b>Field access.</b> <see cref="TagDefinition"/>'s <c>id</c> /
    /// <c>slot</c> / <c>presentation</c> are <c>[SerializeField] private</c>,
    /// populated by Unity's serializer in production. The fallback
    /// <c>InitRuntime</c> setter is <c>internal</c> to the spike asmdef.
    /// Tests live in their own asmdef, so we set the fields via reflection
    /// rather than adding an <c>InternalsVisibleTo</c> attribute to the
    /// spike.</para>
    /// </summary>
    internal static class TagTestSupport
    {
        private static int _next = 70000;

        public static int NextSlot() => Interlocked.Increment(ref _next);

        public static T Make<T>(int slot, string id, TagPresentation? presentation = null)
            where T : TagDefinition
        {
            var inst = ScriptableObject.CreateInstance<T>();
            SetPrivateField(inst, "id", id);
            SetPrivateField(inst, "slot", slot);
            if (presentation != null) SetPrivateField(inst, "presentation", presentation);
            inst.name = $"{typeof(T).Name}_{id}";
            inst.hideFlags = HideFlags.HideAndDontSave;
            return inst;
        }

        public static TagPresentation MakePresentation(string label = "", string tooltip = "", Color? color = null)
        {
            var p = new TagPresentation();
            if (!string.IsNullOrEmpty(label))   SetPrivateField(p, "label", label);
            if (!string.IsNullOrEmpty(tooltip)) SetPrivateField(p, "tooltip", tooltip);
            if (color.HasValue)                 SetPrivateField(p, "color", color.Value);
            return p;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            // Fields live on the abstract non-generic base TagDefinition (for
            // tags) or TagPresentation itself.
            var type = target is TagDefinition ? typeof(TagDefinition) : target.GetType();
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new System.InvalidOperationException(
                    $"Field '{fieldName}' not found on {type.Name}.");
            field.SetValue(target, value);
        }
    }
}
