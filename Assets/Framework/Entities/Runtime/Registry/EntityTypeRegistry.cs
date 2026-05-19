using System;
using System.Collections.Generic;

namespace Framework.Entities
{
    public static class EntityTypeRegistry
    {
        static readonly Dictionary<int, EntityType> _bySlot = new();

        public static EntityType Register(int slot, string name)
        {
            if (_bySlot.ContainsKey(slot))
                throw new InvalidOperationException($"Slot {slot} claimed.");
            var type = new EntityType(slot, name);
            _bySlot[slot] = type;
            return type;
        }

        public static EntityType BySlot(int slot) => _bySlot[slot];

        public static bool TryGetBySlot(int slot, out EntityType type) => _bySlot.TryGetValue(slot, out type);
    }
}
