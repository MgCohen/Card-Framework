#nullable enable
using System;
using UnityEngine;
using VerticalSlice.Engine.Tags;

namespace TagAuthoringPrototype.Authoring
{
    /// <summary>
    /// Pattern C — data-defined tag. ScriptableObject asset that designers
    /// author in the editor. At bake, the asset materializes a
    /// <see cref="TagKey{T}"/> at the designer-picked slot, claiming the
    /// slot in the process-global <see cref="TagRegistry"/> alongside
    /// code-defined Pattern A / Pattern B tags.
    ///
    /// <para>Don't subclass <c>TagDefinition</c> directly — subclass
    /// <see cref="TagDefinition{T}"/> per value type (see <c>IntTag</c>,
    /// <c>BoolTag</c>, etc.). The typed subclass carries an implicit
    /// conversion to <c>TagKey&lt;T&gt;</c> so call sites read identically
    /// to code-defined tags: <c>entity.Get(myIntTag)</c> returns int.</para>
    ///
    /// <para>Slot ranges (organisational convention, mirroring
    /// <c>VerticalSlice.Sample.Tags</c>):</para>
    /// <list type="bullet">
    /// <item>1 – 999  → reserved for code-defined Pattern A + Pattern B</item>
    /// <item>1000+    → data-defined Pattern C (this class)</item>
    /// </list>
    /// </summary>
    public abstract class TagDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("Stable string id. Used as the wire label and as a designer-facing handle. Must be unique across the registry.")]
        string id = "";

        [SerializeField, Tooltip("Runtime slot. Pick a free int in the 1000+ range to avoid colliding with code-defined tags. Collisions throw at bake time.")]
        int slot;

        [SerializeField, Tooltip("Optional UI metadata — icon, label, color. The engine never reads this; UI code looks it up via TagAuthoringRegistry.GetDefinition.")]
        TagPresentation presentation = new();

        public string Id => id;
        public int Slot => slot;
        public TagPresentation Presentation => presentation;
        public abstract Type ValueType { get; }

        /// <summary>
        /// Materialize the <see cref="TagKey"/> for this definition. Calling
        /// this claims the slot in <see cref="TagRegistry"/> (collision =
        /// throw). Idempotent — subsequent calls return the same key.
        /// Called by <c>TagAuthoringRegistry.Bake</c> at app start.
        /// </summary>
        public abstract TagKey Materialize();

        /// <summary>
        /// Runtime-fallback init for the demo's "no asset wired" path. Lets
        /// the demo controller build a <see cref="TagDefinition"/> in
        /// memory via <see cref="ScriptableObject.CreateInstance{T}"/>
        /// without touching reflection. Authoring assets created in the
        /// editor never call this — Unity's serializer populates the
        /// fields from disk.
        /// </summary>
        internal void InitRuntime(string id, int slot, TagPresentation? presentation = null)
        {
            this.id = id;
            this.slot = slot;
            if (presentation != null) this.presentation = presentation;
        }
    }

    /// <summary>
    /// Typed base for data-defined tags. The implicit conversion to
    /// <c>TagKey&lt;T&gt;</c> is what makes <c>entity.Get(myTag)</c> read
    /// as cleanly as <c>entity.Get(Tags.Health)</c>.
    /// </summary>
    public abstract class TagDefinition<T> : TagDefinition
    {
        [NonSerialized] TagKey<T>? _key;

        public TagKey<T> Key => _key
            ?? throw new InvalidOperationException(
                $"TagDefinition '{Id}' (slot {Slot}) is not baked. " +
                "Call TagAuthoringRegistry.Bake(...) at app start.");

        public sealed override Type ValueType => typeof(T);

        public sealed override TagKey Materialize()
        {
            // First Materialize call claims the slot via TagRegistry; later
            // calls are no-ops. The TagKey<T> ctor registers itself with the
            // registry — Pattern C piggybacks on Pattern A's claim path.
            return _key ??= new TagKey<T>(Slot, Id);
        }

        public static implicit operator TagKey<T>(TagDefinition<T> def) => def.Key;
    }
}
