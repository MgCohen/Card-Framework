#nullable enable
using System.Collections.Generic;
using System.Linq;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Stream;
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// Pure substrate. Owns the entity store, the event log, the
    /// monotonic id counter. <b>No verbs.</b> Effect/command dispatch lives
    /// on dedicated services (<c>EffectService</c>, <c>CommandService</c>);
    /// hook subscription lives on <c>CommandService</c>.
    ///
    /// World is intentionally a god-free zone: anyone with a reference can
    /// read entities and the log, but nothing on this surface can mutate
    /// state, fire effects, or dispatch commands. Authors writing inside
    /// effects/commands/hooks reach scoped views (<c>IEffectScope</c>,
    /// <c>ICommandScope</c>, <c>IHookScope</c>) that the services hand them.
    /// </summary>
    public sealed class World
    {
        public EventLog Log { get; } = new();

        private readonly Dictionary<int, Entity> _entities = new();
        private int _nextId = 1;

        public IReadOnlyDictionary<int, Entity> Entities => _entities;

        /// <summary>
        /// Allocate a new entity, register its tag-mutation events to the
        /// log, and emit <see cref="EntityCreatedEvent"/>. Setup-phase
        /// callers use this directly; mid-game allocation typically goes
        /// through a <c>Command</c> that calls into the world via its scope.
        /// </summary>
        public Entity Create()
        {
            var e = new Entity { Id = _nextId++ };
            e.TagSet += OnTagSet;
            e.TagChanged += OnTagChanged;
            _entities[e.Id] = e;
            Log.Append(new EntityCreatedEvent(e.Id));
            return e;
        }

        /// <summary>
        /// Removes an entity and emits <see cref="EntityDestroyedEvent"/>.
        /// Tag events from this entity will no longer flow.
        /// </summary>
        public void Destroy(Entity entity)
        {
            if (!_entities.Remove(entity.Id)) return;
            entity.TagSet -= OnTagSet;
            entity.TagChanged -= OnTagChanged;
            Log.Append(new EntityDestroyedEvent(entity.Id));
        }

        public Entity? Get(int id) => _entities.TryGetValue(id, out var e) ? e : null;

        /// <summary>
        /// All entities, mutable. Used by <c>ICommandScope</c> for queries.
        /// Effects receive the <see cref="IEntity"/>-typed view via their scope.
        /// </summary>
        public IEnumerable<Entity> All() => _entities.Values;

        /// <summary>
        /// Filter by a marker tag (entity has the tag set to a truthy value).
        /// E.g. <c>world.Where(Tags.IsCard)</c> → all entities tagged as cards.
        /// </summary>
        public IEnumerable<Entity> Where(TagKey<bool> marker)
            => _entities.Values.Where(e => e.Has(marker) && e.Get(marker));

        private void OnTagSet(Entity e, TagKey key, object value)
            => Log.Append(new TagSetEvent(e.Id, key.Name, value));

        private void OnTagChanged(Entity e, TagKey key, object prev, object next)
            => Log.Append(new TagChangeEvent(e.Id, key.Name, prev, next));
    }
}
