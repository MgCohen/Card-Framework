#nullable enable
using System.Collections.Generic;
using System.Linq;

namespace StateMachine.Engine
{
    /// <summary>
    /// Minimal world for the spike: a flat entity bag, the Match singleton,
    /// and the wire-event log. Real engine would split these across stores;
    /// here they all live on one object because the focus is the state
    /// machine, not the substrate.
    /// </summary>
    public sealed class World
    {
        public MatchSingleton Match { get; } = new();
        public EventLog       Log   { get; } = new();

        private readonly List<Entity> _entities = new();
        private long _nextId = 1;

        public IReadOnlyList<Entity> Entities => _entities;

        public Entity Create()
        {
            var e = new Entity { Id = _nextId++ };
            _entities.Add(e);
            return e;
        }

        public IEnumerable<Entity> With<T>(TagKey<T> key) =>
            _entities.Where(e => e.Has(key));
    }
}
