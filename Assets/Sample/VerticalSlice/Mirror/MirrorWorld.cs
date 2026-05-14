#nullable enable
using System.Collections.Generic;
using VerticalSlice.Engine.Stream;

namespace VerticalSlice.Mirror
{
    /// <summary>
    /// "Dumb client" — consumes the wire stream and maintains a parallel
    /// state mirror. Has no game-logic dependency on the primary engine
    /// beyond the wire-event types.
    ///
    /// Apply/Unapply are symmetric on every leaf event:
    /// <list type="bullet">
    /// <item><c>EntityCreated</c> ↔ remove from / add to dict.</item>
    /// <item><c>EntityDestroyed</c> ↔ inverse.</item>
    /// <item><c>TagSet</c> (first write) ↔ <b>add the key on Apply, remove
    /// the key on Unapply.</b> This is the fix for "rewinding past a tag's
    /// initial set leaves it stuck at default" — without TagSet vs
    /// TagChange split, the mirror couldn't tell the difference between
    /// "value never existed" and "value was 0".</item>
    /// <item><c>TagChange</c> (subsequent write) ↔ Apply writes Next,
    /// Unapply writes Prev.</item>
    /// </list>
    /// Block events are no-ops for state correctness — they're useful only
    /// for tooling (rendering nesting, attributing causality).
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
                    _entities[c.EntityId] = new MirrorEntity(c.EntityId);
                    break;
                case EntityDestroyedEvent d:
                    _entities.Remove(d.EntityId);
                    break;
                case TagSetEvent ts:
                    if (_entities.TryGetValue(ts.EntityId, out var setEnt))
                        setEnt.Tags[ts.Key] = ts.Value;
                    break;
                case TagChangeEvent tc:
                    if (_entities.TryGetValue(tc.EntityId, out var chgEnt))
                        chgEnt.Tags[tc.Key] = tc.Next;
                    break;
                // BlockStart / BlockEnd — no-op for state.
            }
        }

        public void Unapply(WireEvent ev)
        {
            switch (ev)
            {
                case EntityCreatedEvent c:
                    _entities.Remove(c.EntityId);
                    break;
                case EntityDestroyedEvent d:
                    // Reverse-create: we don't have the original tag values
                    // here. For the slice that's fine — Unapply of Destroy
                    // is only meaningful if the destroyed entity is later
                    // re-applied via subsequent Apply. Game-side responsible
                    // for not destroying entities mid-sequence we'll rewind.
                    _entities[d.EntityId] = new MirrorEntity(d.EntityId);
                    break;
                case TagSetEvent ts:
                    // Reverse of "first write": remove the key entirely so
                    // the mirror is observably "key never existed".
                    if (_entities.TryGetValue(ts.EntityId, out var setEnt))
                        setEnt.Tags.Remove(ts.Key);
                    break;
                case TagChangeEvent tc:
                    if (_entities.TryGetValue(tc.EntityId, out var chgEnt))
                        chgEnt.Tags[tc.Key] = tc.Prev;
                    break;
                // BlockStart / BlockEnd — no-op.
            }
        }

        public void Clear() => _entities.Clear();
    }
}
