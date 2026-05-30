using System.Collections.Generic;

namespace Framework.Entities
{
    // Repository for entities. Pure storage + raw mutation API; never emits.
    // EntityService allocates ids here and emits; apply handlers do the actual Insert,
    // both on the server (where the service emitted) and on the client (where the wire
    // delivered the event). Handler is the sole writer either side.
    public sealed class EntityStore
    {
        readonly Dictionary<int, Entity> _byId = new();
        int _nextId = 1;

        // Server reserves the next id without inserting. Service emits with this id;
        // the EntityCreatedHandler does the matching Insert(id, ...). Splitting allocate
        // from insert lets the service emit-first while still returning the new Entity.
        public int AllocateId() => _nextId++;

        // Auto-id insert — convenience for direct-store callers (tests). Production
        // authoring goes through EntityService.Create.
        public Entity Insert(EntityType type, int definitionId = 0)
        {
            var entity = new Entity(_nextId++, type, definitionId);
            _byId[entity.Id] = entity;
            return entity;
        }

        // Explicit-id insert — idempotent. Apply handlers re-run safely.
        public Entity Insert(int id, EntityType type, int definitionId)
        {
            if (_byId.TryGetValue(id, out var existing)) return existing;
            var entity = new Entity(id, type, definitionId);
            _byId[id] = entity;
            if (id >= _nextId) _nextId = id + 1;
            return entity;
        }

        public Entity ById(int id) => _byId[id];

        public bool TryGetById(int id, out Entity entity) => _byId.TryGetValue(id, out entity);
    }
}
