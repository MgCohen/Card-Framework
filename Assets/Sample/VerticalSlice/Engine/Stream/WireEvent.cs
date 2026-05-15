#nullable enable
using System.Collections.Generic;
using VerticalSlice.Engine.Entities;

namespace VerticalSlice.Engine.Stream
{
    /// <summary>
    /// The slice's wire alphabet — six concrete event kinds, no shared
    /// "Mutation" supertype:
    /// <list type="bullet">
    /// <item><see cref="BlockStartEvent"/> — opens a named scope. Carries a
    /// monotonic <see cref="BlockStartEvent.Id"/>, a <see cref="BlockKind"/>
    /// (set by the dispatch site, not declared on the runnable type), an
    /// optional <see cref="BlockStartEvent.Source"/> entity (provenance),
    /// and a reflection-captured payload of the runnable's properties.</item>
    /// <item><see cref="BlockEndEvent"/> — closes a block, carrying the
    /// matching <see cref="BlockEndEvent.Id"/> and the
    /// <see cref="BlockOutcome"/>.</item>
    /// <item><see cref="EntityCreatedEvent"/> / <see cref="EntityDestroyedEvent"/></item>
    /// <item><see cref="TagSetEvent"/> — first write of a tag key on an
    /// entity. Apply: write key=value. Unapply: <b>remove the key
    /// entirely</b> — fixes init-value rewind.</item>
    /// <item><see cref="TagChangeEvent"/> — subsequent write. Apply:
    /// write Next. Unapply: write Prev.</item>
    /// </list>
    /// </summary>
    public abstract class WireEvent
    {
        public abstract string Kind { get; }
    }

    public sealed class BlockStartEvent : WireEvent
    {
        public long Id { get; }
        public long? ParentId { get; }
        public BlockKind BlockKind { get; }
        public string Name { get; }
        public IEntity? Source { get; }
        public IReadOnlyDictionary<string, object?> Payload { get; }

        public BlockStartEvent(long id, long? parentId, BlockKind blockKind,
                               string name, IEntity? source,
                               IReadOnlyDictionary<string, object?> payload)
        {
            Id = id;
            ParentId = parentId;
            BlockKind = blockKind;
            Name = name;
            Source = source;
            Payload = payload;
        }

        public override string Kind => "BlockStart";
        public override string ToString()
        {
            var src = Source != null ? $", src=#{Source.Id}" : "";
            var parent = ParentId.HasValue ? $", parent=#{ParentId.Value}" : "";
            return $"BlockStart[{Id}] {BlockKind}({Name}{src}{parent})";
        }
    }

    public sealed class BlockEndEvent : WireEvent
    {
        public long Id { get; }
        public BlockOutcome Outcome { get; }

        public BlockEndEvent(long id, BlockOutcome outcome)
        {
            Id = id;
            Outcome = outcome;
        }

        public override string Kind => "BlockEnd";
        public override string ToString()
            => Outcome == BlockOutcome.Completed
                ? $"BlockEnd[{Id}]"
                : $"BlockEnd[{Id}] {Outcome}";
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

    /// <summary>
    /// Tag removal — third state alongside <see cref="TagSetEvent"/> (first
    /// write) and <see cref="TagChangeEvent"/> (subsequent write). Emitted
    /// when <c>Entity.Remove&lt;T&gt;</c> clears the slot entirely.
    /// "Silenced" (<c>Set(false)</c>) is distinct from "removed"
    /// (<c>Remove</c>) — the former leaves the slot present at value
    /// false, the latter strips the slot. Mirror Unapply restores the
    /// previous value at the slot.
    /// </summary>
    public sealed class TagRemovedEvent : WireEvent
    {
        public int EntityId { get; }
        public string Key { get; }
        public object? Prev { get; }

        public TagRemovedEvent(int entityId, string key, object? prev)
        {
            EntityId = entityId;
            Key = key;
            Prev = prev;
        }

        public override string Kind => "TagRemoved";
        public override string ToString()
            => $"TagRemoved(#{EntityId}, {Key}, prev={Prev})";
    }
}
