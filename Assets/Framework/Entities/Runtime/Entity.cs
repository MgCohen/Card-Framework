using System.Collections.Generic;
using Framework.Tags;

namespace Framework.Entities
{
    public sealed class Entity
    {
        readonly Dictionary<int, object> _slots = new();

        public int        Id           { get; }
        public EntityType Type         { get; }
        public int        DefinitionId { get; }

        internal Entity(int id, EntityType type, int definitionId)
        {
            Id = id;
            Type = type;
            DefinitionId = definitionId;
        }

        public void Set<T>(TagKey<T> key, T value) where T : unmanaged => _slots[key.Slot] = value;
        public T    Get<T>(TagKey<T> key)          where T : unmanaged => (T)_slots[key.Slot];
        public bool Has(TagKey key)                                    => _slots.ContainsKey(key.Slot);
        public bool Remove(TagKey key)                                 => _slots.Remove(key.Slot);

        public override string ToString() => $"Entity#{Id}";
    }
}
