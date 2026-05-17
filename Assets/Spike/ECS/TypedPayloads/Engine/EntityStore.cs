#nullable enable
using System.Collections.Generic;

namespace TypedPayloads.Engine
{
    /// <summary>
    /// Owns the entity bag, subscribes to each entity's internal tag events,
    /// and translates them to wire events via the codec registry.
    ///
    /// <para>This is the bridge between "rich in-memory entity" and "pure-data
    /// wire" — the only place that knows how to turn an <c>object</c> tag
    /// value into a <see cref="TagValue"/>.</para>
    /// </summary>
    public sealed class EntityStore
    {
        private readonly EventLog _log;
        private readonly Dictionary<long, Entity> _entities = new();
        private long _nextId = 1;

        public EntityStore(EventLog log) { _log = log; }

        public Entity Create()
        {
            var e = new Entity { Id = _nextId++ };
            e.TagSet     += OnTagSet;
            e.TagChanged += OnTagChanged;
            e.TagRemoved += OnTagRemoved;
            _entities[e.Id] = e;
            _log.Append(new EntityCreatedEvent(e.Id));
            return e;
        }

        public void Destroy(Entity e)
        {
            if (!_entities.Remove(e.Id)) return;
            e.TagSet     -= OnTagSet;
            e.TagChanged -= OnTagChanged;
            e.TagRemoved -= OnTagRemoved;
            _log.Append(new EntityDestroyedEvent(e.Id));
        }

        public Entity? Get(long id) => _entities.TryGetValue(id, out var e) ? e : null;

        // --- Entity-event handlers — the codec lookup happens here -------

        private void OnTagSet(Entity e, TagKey key, object value)
        {
            var wire = TagCodecs.Encode(key, value);
            _log.Append(new TagSetEvent(e.Id, key.Slot, wire));
        }

        private void OnTagChanged(Entity e, TagKey key, object prev, object next)
        {
            var wirePrev = TagCodecs.Encode(key, prev);
            var wireNext = TagCodecs.Encode(key, next);
            _log.Append(new TagChangeEvent(e.Id, key.Slot, wirePrev, wireNext));
        }

        private void OnTagRemoved(Entity e, TagKey key, object prev)
        {
            var wire = TagCodecs.Encode(key, prev);
            _log.Append(new TagRemovedEvent(e.Id, key.Slot, wire));
        }
    }
}
