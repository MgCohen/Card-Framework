#nullable enable
using System.Collections.Generic;

namespace Spike.EntitySetup
{
    /// <summary>
    /// The single concrete entity type. Three first-class identity fields
    /// (Id, Type, DefinitionId — HS-shape) plus a sparse typed tag bag.
    ///
    /// <para><b>Id</b> — instance identity. Allocated by
    /// <see cref="EntityStore.Create"/>.</para>
    ///
    /// <para><b>Type</b> — catalog partition. Tells whichever catalog dict
    /// to look in when resolving <see cref="DefinitionId"/>.</para>
    ///
    /// <para><b>DefinitionId</b> — content reference. <c>0</c> means
    /// defless (zones, players, system entities). Non-zero indexes into
    /// the typed catalog selected by <see cref="Type"/>.</para>
    ///
    /// <para>This spike strips the production Entity to its essentials —
    /// no wire events, no internal store-side accessors beyond
    /// <see cref="Slots"/>. The point is to demonstrate the construction
    /// flow, not to re-implement the indexed query path.</para>
    /// </summary>
    public sealed class Entity
    {
        public int        Id           { get; internal set; }
        public EntityType Type         { get; internal set; }
        public int        DefinitionId { get; internal set; }

        private readonly Dictionary<int, object?> _slots = new();

        public T Get<T>(TagKey<T> key)
        {
            if (_slots.TryGetValue(key.Slot, out var raw) && raw is T t) return t;
            return default!;
        }
        public bool Has<T>(TagKey<T> key) => _slots.ContainsKey(key.Slot);
        public void Set<T>(TagKey<T> key, T value) => _slots[key.Slot] = value;

        /// <summary>
        /// Untyped slot bag access for store / query plumbing.
        /// Public surface stays <c>Get&lt;T&gt;(TagKey&lt;T&gt;)</c>.
        /// </summary>
        internal IReadOnlyDictionary<int, object?> Slots => _slots;

        public override string ToString() => $"Entity#{Id}";
    }
}
