#nullable enable
using System.Collections.Generic;

namespace VerticalSlice.Engine.Stream
{
    /// <summary>
    /// The slice's wire alphabet — six concrete event kinds, no shared
    /// "Mutation" supertype:
    /// <list type="bullet">
    /// <item><see cref="BlockStartEvent"/> — opens a named scope; payload
    /// captures the runnable's reflected properties.</item>
    /// <item><see cref="BlockEndEvent"/> — closes the most-recent unclosed
    /// block (LIFO).</item>
    /// <item><see cref="EntityCreatedEvent"/> — entity allocated.</item>
    /// <item><see cref="EntityDestroyedEvent"/> — entity removed.</item>
    /// <item><see cref="TagSetEvent"/> — first write of a tag key on an
    /// entity. Apply: write the value, mirror entity now <i>has</i> the key.
    /// Unapply: <b>remove the key entirely</b> (this is what fixes the
    /// init-value rewind bug).</item>
    /// <item><see cref="TagChangeEvent"/> — subsequent write of an
    /// already-set key. Apply: write Next. Unapply: write Prev.</item>
    /// </list>
    /// Block kind (Play / Trigger / System / etc.) is not in the wire —
    /// it's recoverable from the BlockStart's <see cref="BlockStartEvent.Name"/>
    /// via a runtime registry if needed. Most consumers don't care.
    /// </summary>
    public abstract class WireEvent
    {
        public abstract string Kind { get; }
    }

    public sealed class BlockStartEvent : WireEvent
    {
        public string Name { get; }
        public IReadOnlyDictionary<string, object?> Payload { get; }

        public BlockStartEvent(string name, IReadOnlyDictionary<string, object?> payload)
        {
            Name = name;
            Payload = payload;
        }

        public override string Kind => "BlockStart";
        public override string ToString() => $"BlockStart({Name})";
    }

    public sealed class BlockEndEvent : WireEvent
    {
        public override string Kind => "BlockEnd";
        public override string ToString() => "BlockEnd";
    }

    public sealed class EntityCreatedEvent : WireEvent
    {
        public int EntityId { get; }
        public EntityCreatedEvent(int entityId) { EntityId = entityId; }
        public override string Kind => "EntityCreated";
        public override string ToString() => $"EntityCreated(#{EntityId})";
    }

    public sealed class EntityDestroyedEvent : WireEvent
    {
        public int EntityId { get; }
        public EntityDestroyedEvent(int entityId) { EntityId = entityId; }
        public override string Kind => "EntityDestroyed";
        public override string ToString() => $"EntityDestroyed(#{EntityId})";
    }

    public sealed class TagSetEvent : WireEvent
    {
        public int EntityId { get; }
        public string Key { get; }
        public object? Value { get; }

        public TagSetEvent(int entityId, string key, object? value)
        {
            EntityId = entityId;
            Key = key;
            Value = value;
        }

        public override string Kind => "TagSet";
        public override string ToString() => $"TagSet(#{EntityId}, {Key}, {Value})";
    }

    public sealed class TagChangeEvent : WireEvent
    {
        public int EntityId { get; }
        public string Key { get; }
        public object? Prev { get; }
        public object? Next { get; }

        public TagChangeEvent(int entityId, string key, object? prev, object? next)
        {
            EntityId = entityId;
            Key = key;
            Prev = prev;
            Next = next;
        }

        public override string Kind => "TagChange";
        public override string ToString()
            => $"TagChange(#{EntityId}, {Key}, {Prev} -> {Next})";
    }
}
