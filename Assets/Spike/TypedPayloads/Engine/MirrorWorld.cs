#nullable enable
using System.Collections.Generic;

namespace TypedPayloads.Engine
{
    /// <summary>
    /// Dumb-consumer side: walks the wire stream and maintains a parallel
    /// entity bag whose tag values are <see cref="TagValue"/> instances (not
    /// decoded). Consumers calling <see cref="MirrorEntity.Get{T}"/> trigger
    /// type-directed decoding on read.
    ///
    /// <para>Apply/Unapply are symmetric per event kind. TagSet rewind
    /// removes the slot; TagRemoved rewind restores the slot to its
    /// previous value.</para>
    /// </summary>
    public sealed class MirrorWorld
    {
        private readonly Dictionary<long, MirrorEntity> _entities = new();

        public IReadOnlyDictionary<long, MirrorEntity> Entities => _entities;
        public MirrorEntity? Get(long id) => _entities.TryGetValue(id, out var e) ? e : null;

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
                        setEnt.Tags[ts.Slot] = ts.Value;
                    break;
                case TagChangeEvent tc:
                    if (_entities.TryGetValue(tc.EntityId, out var chgEnt))
                        chgEnt.Tags[tc.Slot] = tc.Next;
                    break;
                case TagRemovedEvent tr:
                    if (_entities.TryGetValue(tr.EntityId, out var remEnt))
                        remEnt.Tags.Remove(tr.Slot);
                    break;
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
                    // For the spike: re-create empty. Rewinding a destroy
                    // perfectly would require carrying the prior tag bag on
                    // the wire — out of scope here.
                    _entities[d.EntityId] = new MirrorEntity(d.EntityId);
                    break;
                case TagSetEvent ts:
                    if (_entities.TryGetValue(ts.EntityId, out var setEnt))
                        setEnt.Tags.Remove(ts.Slot);
                    break;
                case TagChangeEvent tc:
                    if (_entities.TryGetValue(tc.EntityId, out var chgEnt))
                        chgEnt.Tags[tc.Slot] = tc.Prev;
                    break;
                case TagRemovedEvent tr:
                    if (_entities.TryGetValue(tr.EntityId, out var remEnt))
                        remEnt.Tags[tr.Slot] = tr.Prev;
                    break;
            }
        }

        public void Clear() => _entities.Clear();
    }
}
