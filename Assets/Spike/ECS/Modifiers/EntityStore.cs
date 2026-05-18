#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace Spike.Modifiers
{
    public sealed class EntityStore
    {
        private readonly Dictionary<int, Entity> _entities = new();
        private int _nextId = 1;

        public Entity Create(EntityType type, int defId = 0)
        {
            var e = new Entity { Id = _nextId++, Type = type, DefinitionId = defId };
            _entities[e.Id] = e;
            return e;
        }

        public Entity? Get(int id) => _entities.TryGetValue(id, out var e) ? e : null;
        public void    Destroy(int id) => _entities.Remove(id);

        public IReadOnlyDictionary<int, Entity> All => _entities;

        // Modifier-entity queries. Linear scan — fine for spike scale.
        // Production would maintain (ModTarget, Order) and ModSource indices.
        public IEnumerable<Entity> ModifiersTargeting(int targetId)
            => _entities.Values.Where(e =>
                e.Type.Equals(EntityTypes.Modifier) &&
                e.Has(Tags.ModTarget) &&
                e.Get(Tags.ModTarget) == targetId);

        public IEnumerable<Entity> ModifiersFromSource(int sourceId)
            => _entities.Values.Where(e =>
                e.Type.Equals(EntityTypes.Modifier) &&
                e.Has(Tags.ModSource) &&
                e.Get(Tags.ModSource) == sourceId);
    }
}
