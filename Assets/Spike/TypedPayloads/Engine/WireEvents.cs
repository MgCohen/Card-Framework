#nullable enable
namespace TypedPayloads.Engine
{
    /// <summary>
    /// Base for pure-data wire records. No <c>Apply</c>, no <c>Revert</c>,
    /// no visitor — consumers (Mirror, replay viewer, network analyzer) each
    /// switch on the concrete type independently.
    /// </summary>
    public abstract class WireEvent
    {
        public abstract WireKind Kind { get; }
    }

    public sealed class EntityCreatedEvent : WireEvent
    {
        public long EntityId { get; }
        public EntityCreatedEvent(long id) { EntityId = id; }
        public override WireKind Kind => WireKind.EntityCreated;
        public override string ToString() => $"EntityCreated(#{EntityId})";
    }

    public sealed class EntityDestroyedEvent : WireEvent
    {
        public long EntityId { get; }
        public EntityDestroyedEvent(long id) { EntityId = id; }
        public override WireKind Kind => WireKind.EntityDestroyed;
        public override string ToString() => $"EntityDestroyed(#{EntityId})";
    }

    /// <summary>
    /// First write of a tag on an entity. Apply: write key=value. Unapply:
    /// REMOVE the key entirely — distinct from "set to default".
    /// </summary>
    public sealed class TagSetEvent : WireEvent
    {
        public long     EntityId { get; }
        public int      Slot     { get; }
        public TagValue Value    { get; }
        public TagSetEvent(long entityId, int slot, TagValue value)
        { EntityId = entityId; Slot = slot; Value = value; }
        public override WireKind Kind => WireKind.TagSet;
        public override string ToString() => $"TagSet(#{EntityId}, slot={Slot}, {Value})";
    }

    /// <summary>
    /// Subsequent write of a tag. Apply: write Next. Unapply: write Prev.
    /// </summary>
    public sealed class TagChangeEvent : WireEvent
    {
        public long     EntityId { get; }
        public int      Slot     { get; }
        public TagValue Prev     { get; }
        public TagValue Next     { get; }
        public TagChangeEvent(long entityId, int slot, TagValue prev, TagValue next)
        { EntityId = entityId; Slot = slot; Prev = prev; Next = next; }
        public override WireKind Kind => WireKind.TagChange;
        public override string ToString() => $"TagChange(#{EntityId}, slot={Slot}, {Prev} -> {Next})";
    }

    /// <summary>
    /// Tag removed entirely (slot stripped). Apply: drop the key. Unapply:
    /// restore <see cref="Prev"/> at the slot.
    /// </summary>
    public sealed class TagRemovedEvent : WireEvent
    {
        public long     EntityId { get; }
        public int      Slot     { get; }
        public TagValue Prev     { get; }
        public TagRemovedEvent(long entityId, int slot, TagValue prev)
        { EntityId = entityId; Slot = slot; Prev = prev; }
        public override WireKind Kind => WireKind.TagRemoved;
        public override string ToString() => $"TagRemoved(#{EntityId}, slot={Slot}, prev={Prev})";
    }
}
