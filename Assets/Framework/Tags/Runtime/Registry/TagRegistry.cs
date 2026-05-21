using System;
using System.Collections.Generic;

namespace Framework.Tags
{
    public static class TagRegistry
    {
        static readonly Dictionary<int, TagKey> _bySlot = new();

        public static void RegisterInstance(TagKey key)
        {
            if (_bySlot.TryGetValue(key.Slot, out var existing))
            {
                if (ReferenceEquals(existing, key)) return;
                throw new InvalidOperationException(
                    $"Slot {key.Slot} double-claimed (existing: {existing}, new: {key})");
            }
            _bySlot[key.Slot] = key;
        }

        public static TagKey BySlot(int slot) => _bySlot[slot];

        public static bool TryGetBySlot(int slot, out TagKey key) => _bySlot.TryGetValue(slot, out key);

        public static IReadOnlyCollection<TagKey> All => _bySlot.Values;
    }
}
