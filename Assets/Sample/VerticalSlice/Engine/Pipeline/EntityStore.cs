#nullable enable
using System.Collections.Generic;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Stream;
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// Owns the entity bag and its tag-indexed views. Sole reader/writer
    /// of entity state — <see cref="World"/> shrinks to substrate
    /// (<see cref="World.Log"/> + block tracking); everything else lives
    /// here.
    ///
    /// <para><b>Why this is a separate service.</b> Two reasons. First,
    /// queries are the hot path — triggers, SBAs, targeting all re-ask
    /// the same shapes constantly, and a per-query full-table scan
    /// burned both CPU and allocations. The store maintains tag-indexed
    /// buckets (<see cref="TagIndex"/>) so an <c>scope.With(IsMinion).And(Zone, P1Board)</c>
    /// iterates the smallest indexed set, not the whole world. Second,
    /// the public surface has <b>no</b> <see cref="IEnumerable{T}"/> exit
    /// for entities — every read path goes through <see cref="With"/> /
    /// <see cref="Where{T}"/> / <see cref="Get"/>. No LINQ side door means
    /// the index is a hard invariant, not a fast path callers may
    /// forget. (See <c>Product/spikes/entity-store-extraction.md</c> for
    /// the bench results that drove the shape — 10.3-10.9× LINQ at
    /// Hearthstone-sized populations, ~5% mutation overhead.)</para>
    ///
    /// <para><b>Index registration is opt-in</b> and declared at
    /// bootstrap. Tags without a registered index still work for Get /
    /// Set on entities; they just don't get a query path. Add an index
    /// when you need to query by that tag.</para>
    ///
    /// <para><b>Authors don't touch this directly.</b> They reach it
    /// through <c>ICommandScope</c> / <c>IEffectScope</c>'s
    /// <c>With</c> / <c>Where</c> / <c>Get</c> — the scope forwards
    /// here. The store is exposed on <c>GameEngine</c> mostly for
    /// bootstrap setup and tests.</para>
    /// </summary>
    public sealed class EntityStore
    {
        private readonly EventLog _log;
        private readonly Dictionary<int, Entity> _entities = new();
        private readonly TagIndex _index = new();
        private int _nextId = 1;

        public EntityStore(EventLog log) { _log = log; }

        // --- Index setup (bootstrap) ----------------------------------------

        /// <summary>
        /// Register a marker index on a <c>bool</c> tag. The index holds
        /// every entity whose tag value is currently <c>true</c> — flipping
        /// to <c>false</c> removes the id from the bucket.
        /// </summary>
        public void IndexMarker(TagKey<bool> key) => _index.RegisterMarker(key.Slot);

        /// <summary>
        /// Register a value index on a typed tag. The index buckets entity
        /// ids by current value (e.g. <c>Zone</c> → ids in that zone).
        /// </summary>
        public void IndexBy<T>(TagKey<T> key) => _index.RegisterValue(key.Slot);

        // --- Entity lifecycle ------------------------------------------------

        /// <summary>
        /// Allocate a new entity, wire up its tag-mutation events, log
        /// <see cref="EntityCreatedEvent"/>. Mid-game callers reach this
        /// through a command's <c>ICommandScope</c>; setup callers
        /// (<c>SliceBootstrap</c>, tests) call it directly on the store.
        /// </summary>
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

        /// <summary>
        /// Remove an entity, clean up its index entries, log
        /// <see cref="EntityDestroyedEvent"/>. Tag events from this
        /// entity stop flowing — detached writes won't emit further.
        /// </summary>
        public void Destroy(Entity entity)
        {
            if (!_entities.Remove(entity.Id)) return;
            _index.OnEntityDestroyed(entity.Id, entity.SnapshotSlots());
            entity.TagSet     -= OnTagSet;
            entity.TagChanged -= OnTagChanged;
            entity.TagRemoved -= OnTagRemoved;
            _log.Append(new EntityDestroyedEvent(entity.Id));
        }

        public Entity? Get(int id) => _entities.TryGetValue(id, out var e) ? e : null;

        // --- Queries ---------------------------------------------------------

        /// <summary>Active-marker query: ids whose <paramref name="marker"/> is <c>true</c>.</summary>
        public EntityQuery With(TagKey<bool> marker)
        {
            _index.TryGetMarker(marker.Slot, out var set);
            return new EntityQuery(_entities, _index,
                new EntityQuery.Predicate(marker.Slot, null, true, set ?? TagIndex.EmptySet));
        }

        /// <summary>Value-equals query: ids whose <paramref name="key"/> currently equals <paramref name="value"/>.</summary>
        public EntityQuery Where<T>(TagKey<T> key, T value) where T : notnull
        {
            object boxed = value;
            _index.TryGetValueBucket(key.Slot, boxed, out var set);
            return new EntityQuery(_entities, _index,
                new EntityQuery.Predicate(key.Slot, boxed, false, set));
        }

        // --- Diagnostics-only enumeration -----------------------------------

        /// <summary>
        /// Tooling / debug enumeration of every live entity. <b>Not for
        /// gameplay code</b> — gameplay paths must use <see cref="With"/> /
        /// <see cref="Where{T}"/> so indexed queries stay the only path
        /// and indices remain a hard invariant. Bootstrap UIs, test
        /// assertions, and replay tooling are the legitimate callers.
        /// </summary>
        public IReadOnlyDictionary<int, Entity> EntitiesForDiagnostics => _entities;

        // --- Event handlers (subscribed per entity in Create) ---------------

        private void OnTagSet(Entity e, TagKey key, object value)
        {
            _log.Append(new TagSetEvent(e.Id, key.Name, value));
            _index.OnTagSet(e.Id, key.Slot, value);
        }

        private void OnTagChanged(Entity e, TagKey key, object prev, object next)
        {
            _log.Append(new TagChangeEvent(e.Id, key.Name, prev, next));
            _index.OnTagChanged(e.Id, key.Slot, prev, next);
        }

        private void OnTagRemoved(Entity e, TagKey key, object prev)
        {
            _log.Append(new TagRemovedEvent(e.Id, key.Name, prev));
            _index.OnTagRemoved(e.Id, key.Slot, prev);
        }
    }
}
