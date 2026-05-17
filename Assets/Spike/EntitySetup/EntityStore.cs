#nullable enable
using System.Collections.Generic;

namespace Spike.EntitySetup
{
    /// <summary>
    /// Minimal store for the setup spike. Allocates instance ids, holds
    /// every live entity, exposes diagnostics enumeration.
    ///
    /// <para>No indexing, no query path, no wire emission — those exist
    /// in the production VerticalSlice's EntityStore but are out of scope
    /// for the construction-flow validation. The point here is to show
    /// the layered Spawn API working end-to-end against the simplest
    /// store that satisfies it.</para>
    /// </summary>
    public sealed class EntityStore
    {
        private readonly Dictionary<int, Entity> _entities = new();
        private int _nextId = 1;

        /// <summary>
        /// Layer 1 — the defless primitive. Allocates id, stamps type +
        /// definition id, registers in the lookup. Caller writes tags.
        /// </summary>
        public Entity Create(EntityType type, int defId = 0)
        {
            var e = new Entity { Id = _nextId++, Type = type, DefinitionId = defId };
            _entities[e.Id] = e;
            return e;
        }

        public Entity? Get(int id) => _entities.TryGetValue(id, out var e) ? e : null;
        public void    Destroy(int id) => _entities.Remove(id);

        public IReadOnlyDictionary<int, Entity> All => _entities;
    }
}
