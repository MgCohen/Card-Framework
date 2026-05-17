#nullable enable
namespace TypedPayloads.Engine
{
    /// <summary>
    /// Discriminator for <see cref="WireEvent"/>. Byte-sized so a future
    /// custom binary serializer can use it as a single-byte tag prefix.
    /// </summary>
    public enum WireKind : byte
    {
        EntityCreated,
        EntityDestroyed,
        TagSet,
        TagChange,
        TagRemoved,
    }
}
