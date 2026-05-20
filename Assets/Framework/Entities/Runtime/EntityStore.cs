using System.Collections.Generic;

namespace Framework.Entities
{
    public sealed class EntityStore
    {
        readonly Dictionary<int, Entity> _byId = new();
        int _nextId = 1;

        public Entity Create(EntityType type, int definitionId = 0)
        {
            var entity = new Entity(_nextId++, type, definitionId);
            _byId[entity.Id] = entity;
            return entity;
        }

        public Entity ById(int id) => _byId[id];

        public bool TryGetById(int id, out Entity entity) => _byId.TryGetValue(id, out entity);
    }
}
