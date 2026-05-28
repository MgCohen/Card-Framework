using Scaffold.Maps;

namespace Framework.Tags
{
    // Pure repository over (entityId, slot) → boxed value. No emit, no event awareness.
    // Apply handlers call into this directly; TagService also goes through it for raw writes.
    public sealed class TagStore
    {
        readonly Map<int, int, object> _tags = new();

        public T Get<T>(int entityId, int slot) where T : unmanaged => (T)_tags[entityId, slot];

        public bool Has(int entityId, int slot) => _tags.Contains(entityId, slot);

        public void Set<T>(int entityId, int slot, T value) where T : unmanaged
            => _tags[entityId, slot] = value;

        public bool Remove(int entityId, int slot) => _tags.Remove(entityId, slot);
    }
}
