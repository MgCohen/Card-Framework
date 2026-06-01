using System.Collections.Generic;

namespace Framework.Entities
{
    // Repository for entities. Pure storage; never allocates ids, never emits. EntityService
    // allocates the id and emits; the EntityCreatedHandler does the Insert — both on the server
    // (where the service emitted) and on the client (where the wire delivered the event). The
    // handler is the sole writer either side, so Insert is internal: only the feature's own
    // service/handler may write; everyone else reads.
    public sealed class EntityStore : IReadOnlyEntityStore
    {
        readonly Dictionary<int, Entity> _byId = new();

        // Explicit-id insert — idempotent. Apply handlers re-run safely (wire replay).
        internal Entity Insert(int id, EntityType type, int definitionId)
        {
            if (_byId.TryGetValue(id, out var existing)) return existing;
            var entity = new Entity(id, type, definitionId);
            _byId[id] = entity;
            return entity;
        }

        public Entity ById(int id) => _byId[id];

        public bool TryGetById(int id, out Entity entity) => _byId.TryGetValue(id, out entity);
    }
}
