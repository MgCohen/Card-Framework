#nullable enable
using System.Collections.Generic;
using VerticalSlice.Engine.Stream;

namespace VerticalSlice.Mirror
{
    /// <summary>
    /// The "dumb client" — consumes the wire stream, maintains a parallel
    /// state mirror. Has no game-logic dependency on the primary engine
    /// beyond the wire-event types.
    ///
    /// Apply/Unapply are symmetric: <c>Apply(TagChange)</c> writes Next,
    /// <c>Unapply</c> writes Prev. <c>EntityCreated</c> creates the mirror
    /// entity forward; the reverse removes it.
    /// </summary>
    public sealed class MirrorWorld
    {
        private readonly Dictionary<int, MirrorEntity> _entities = new();

        public IReadOnlyDictionary<int, MirrorEntity> Entities => _entities;
        public bool Contains(int id) => _entities.ContainsKey(id);
        public MirrorEntity? Get(int id)
            => _entities.TryGetValue(id, out var e) ? e : null;

        public void Apply(WireEvent ev)
        {
            switch (ev)
            {
                case EntityCreatedEvent c:
                    _entities[c.EntityId] = new MirrorEntity(c.EntityId, c.TypeName);
                    break;
                case TagChangeEvent tc:
                    if (_entities.TryGetValue(tc.EntityId, out var ent))
                        ent.Tags[tc.Key] = tc.Next;
                    break;
                // BlockStart / BlockEnd: no-op for state correctness.
            }
        }

        public void Unapply(WireEvent ev)
        {
            switch (ev)
            {
                case EntityCreatedEvent c:
                    _entities.Remove(c.EntityId);
                    break;
                case TagChangeEvent tc:
                    if (_entities.TryGetValue(tc.EntityId, out var ent))
                        ent.Tags[tc.Key] = tc.Prev;
                    break;
                // BlockStart / BlockEnd: no-op.
            }
        }

        public void Clear() => _entities.Clear();
    }
}
