#nullable enable
using System;
using System.Collections.Generic;
using VerticalSlice.Engine.Tags;

namespace TagAuthoringPrototype.Authoring
{
    /// <summary>
    /// Prototype-side companion to <see cref="TagRegistry"/>. Holds the
    /// slot → <see cref="TagDefinition"/> lookup so UI code can resolve a
    /// tag's presentation metadata from its key. The engine's
    /// <c>TagRegistry</c> only knows about <c>TagKey</c>s; this layer adds
    /// the def-side lookup without touching the engine.
    ///
    /// <para><b>Bake once at app start.</b> Pass every <see cref="TagDefinition"/>
    /// asset you want the slice to know about. Each Materialize call claims
    /// its slot in the underlying <c>TagRegistry</c> (collision throws).
    /// Subsequent bakes of the same asset are idempotent.</para>
    /// </summary>
    public static class TagAuthoringRegistry
    {
        static readonly Dictionary<int, TagDefinition>    _bySlot = new();
        static readonly Dictionary<string, TagDefinition> _byId   = new();

        public static int Count => _bySlot.Count;
        public static IEnumerable<TagDefinition> All => _bySlot.Values;

        /// <summary>
        /// Materializes every definition in <paramref name="defs"/> and
        /// stores it for lookup. Skips nulls. Idempotent.
        /// </summary>
        public static void Bake(IEnumerable<TagDefinition?> defs)
        {
            if (defs == null) return;
            foreach (var def in defs)
            {
                if (def == null) continue;
                if (string.IsNullOrEmpty(def.Id))
                    throw new InvalidOperationException(
                        $"TagDefinition asset '{def.name}' has empty Id. Set the Id field in the inspector.");
                if (def.Slot <= 0)
                    throw new InvalidOperationException(
                        $"TagDefinition '{def.Id}' (asset '{def.name}') has invalid slot {def.Slot}. Pick a free int in the 1000+ range.");

                def.Materialize();                // claims slot via TagRegistry
                _bySlot[def.Slot] = def;
                _byId[def.Id] = def;
            }
        }

        /// <summary>Resolve a data-defined tag's authoring asset from its key. Null for code-defined (Pattern A / B) tags.</summary>
        public static TagDefinition? GetDefinition(TagKey key)
            => _bySlot.TryGetValue(key.Slot, out var d) ? d : null;

        public static TagDefinition? GetDefinition(int slot)
            => _bySlot.TryGetValue(slot, out var d) ? d : null;

        public static TagDefinition? GetDefinition(string id)
            => _byId.TryGetValue(id, out var d) ? d : null;

        /// <summary>Test-only — clears the prototype-side def map. Does NOT reset the engine's <see cref="TagRegistry"/>.</summary>
        public static void ResetForTests()
        {
            _bySlot.Clear();
            _byId.Clear();
        }
    }
}
