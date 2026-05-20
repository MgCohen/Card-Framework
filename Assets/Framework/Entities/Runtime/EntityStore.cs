using System;
using System.Collections.Generic;

namespace Framework.Entities
{
    public sealed class EntityStore
    {
        readonly Dictionary<int, Entity> _byId = new();
        int _nextId = 1;

        public Entity Create(EntityType type, int definitionId = 0)
            => Create(_nextId, type, definitionId);

        public Entity Create(int id, EntityType type, int definitionId)
        {
            if (_byId.ContainsKey(id))
                throw new InvalidOperationException($"Id {id} claimed.");
            var entity = new Entity(id, type, definitionId);
            _byId[id] = entity;
            if (id >= _nextId) _nextId = id + 1;
            return entity;
        }

        public Entity ById(int id) => _byId[id];

        public bool TryGetById(int id, out Entity entity) => _byId.TryGetValue(id, out entity);
    }
}
