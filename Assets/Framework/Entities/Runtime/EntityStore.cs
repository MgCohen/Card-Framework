using System.Collections.Generic;
using Framework.Tags;

namespace Framework.Entities
{
    // Repository for entities. Pure storage + raw mutation API; never emits.
    // EntityService wraps this with emit logic. Apply handlers call this directly to mirror
    // server-side changes without re-emitting.
    public sealed class EntityStore
    {
        readonly Dictionary<int, Entity> _byId = new();
        readonly TagService _tags;
        int _nextId = 1;

        public EntityStore(TagService tags) { _tags = tags; }

        // Auto-id insert — server allocates ids.
        public Entity Insert(EntityType type, int definitionId = 0)
        {
            var entity = new Entity(_nextId++, type, definitionId, _tags);
            _byId[entity.Id] = entity;
            return entity;
        }

        // Explicit-id insert — idempotent. Apply handlers re-run safely; if the entity
        // already exists (server-side, where EntityService.Create already inserted) the
        // existing instance is returned.
        public Entity Insert(int id, EntityType type, int definitionId)
        {
            if (_byId.TryGetValue(id, out var existing)) return existing;
            var entity = new Entity(id, type, definitionId, _tags);
            _byId[id] = entity;
            if (id >= _nextId) _nextId = id + 1;
            return entity;
        }

        public Entity ById(int id) => _byId[id];

        public bool TryGetById(int id, out Entity entity) => _byId.TryGetValue(id, out entity);
    }
}
