#nullable enable
using System.Collections.Generic;

namespace VerticalSlice.Engine.Tags
{
    /// <summary>
    /// Process-global slot allocator for <see cref="TagKey"/> instances.
    /// Slice scope only — production should scope this per-World so that
    /// app-domain reloads don't drift the slot indices.
    /// </summary>
    public static class TagRegistry
    {
        private static int _nextSlot;
        private static readonly List<TagKey> _keys = new();

        public static int Capacity => _nextSlot;

        public static int Register(TagKey key)
        {
            var slot = _nextSlot++;
            _keys.Add(key);
            return slot;
        }

        public static TagKey? GetBySlot(int slot)
            => slot >= 0 && slot < _keys.Count ? _keys[slot] : null;

        public static IReadOnlyList<TagKey> All => _keys;
    }
}
