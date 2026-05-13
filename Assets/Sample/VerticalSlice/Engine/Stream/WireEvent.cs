#nullable enable
using System.Collections.Generic;
using VerticalSlice.Engine.Pipeline;

namespace VerticalSlice.Engine.Stream
{
    /// <summary>
    /// Base class for all log entries. The slice's wire alphabet is four
    /// kinds: <see cref="BlockStartEvent"/>, <see cref="BlockEndEvent"/>,
    /// <see cref="TagChangeEvent"/>, <see cref="EntityCreatedEvent"/>.
    /// </summary>
    public abstract class WireEvent
    {
        public abstract string Kind { get; }
    }

    public sealed class BlockStartEvent : WireEvent
    {
        public BlockType Type { get; }
        public string? CommandTypeName { get; }
        public IReadOnlyDictionary<string, object?> Payload { get; }

        public BlockStartEvent(BlockType type, string? commandTypeName,
                               IReadOnlyDictionary<string, object?> payload)
        {
            Type = type;
            CommandTypeName = commandTypeName;
            Payload = payload;
        }

        public override string Kind => "BlockStart";
        public override string ToString()
            => $"BlockStart({Type}, {CommandTypeName ?? "—"})";
    }

    public sealed class BlockEndEvent : WireEvent
    {
        public BlockType Type { get; }
        public BlockEndEvent(BlockType type) { Type = type; }
        public override string Kind => "BlockEnd";
        public override string ToString() => $"BlockEnd({Type})";
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

    public sealed class EntityCreatedEvent : WireEvent
    {
        public int EntityId { get; }
        public string TypeName { get; }

        public EntityCreatedEvent(int entityId, string typeName)
        {
            EntityId = entityId;
            TypeName = typeName;
        }

        public override string Kind => "EntityCreated";
        public override string ToString() => $"EntityCreated(#{EntityId}, {TypeName})";
    }
}
