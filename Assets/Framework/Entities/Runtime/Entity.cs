using Framework.Tags;

namespace Framework.Entities
{
    // Data + ergonomic façade. Tag storage lives on TagService (which delegates to TagStore).
    // The back-ref to TagService is what lets `entity.Set(...)` emit; reads pass through too.
    //
    // No-bounce note: Apply handlers should work by id (TagSetHandler<T>.Apply does
    // `_store.Set(ev.EntityId, ev.Slot, ev.Value)` directly, no Entity instance involved).
    // If a handler did somehow call `entity.Set(...)`, TagService.Set equality-short-circuits
    // a no-op write and the recursion dies at the no-change check.
    public sealed class Entity
    {
        readonly TagService _tags;

        public int        Id           { get; }
        public EntityType Type         { get; }
        public int        DefinitionId { get; }

        internal Entity(int id, EntityType type, int definitionId, TagService tags)
        {
            Id = id;
            Type = type;
            DefinitionId = definitionId;
            _tags = tags;
        }

        public T    Get<T>(TagKey<T> key)          where T : unmanaged => _tags.Get(Id, key);
        public void Set<T>(TagKey<T> key, T value) where T : unmanaged => _tags.Set(Id, key, value);
        public bool Has(TagKey key)                                    => _tags.Has(Id, key);
        public bool Remove(TagKey key)                                 => _tags.Remove(Id, key);

        public override string ToString() => $"Entity#{Id}";
    }
}
