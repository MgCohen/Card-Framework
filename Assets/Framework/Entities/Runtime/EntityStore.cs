using System.Collections.Generic;

namespace Framework.Entities
{
    public sealed class EntityStore : IReadOnlyEntityStore
    {
        readonly Dictionary<int, Entity> _byId = new();

        // Apply-handler is the sole writer (internal); idempotent on wire replay.
        internal Entity Insert(int id, EntityType type, int definitionId)
        {
            if (_byId.TryGetValue(id, out var existing)) return existing;
            var entity = new Entity(id, type, definitionId);
            _byId[id] = entity;
            return entity;
        }

        public Entity ById(int id)
            => _byId.TryGetValue(id, out var entity) ? entity : throw new KeyNotFoundException($"No entity with id {id}.");

        public bool TryGetById(int id, out Entity entity) => _byId.TryGetValue(id, out entity);
    }
}
