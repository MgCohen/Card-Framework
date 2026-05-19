using Scaffold.AutoPacker;
using Unity.Netcode;

namespace Framework.Wire
{
    /// <summary>
    /// Root of the wire alphabet. Every event that crosses the wire derives — directly or via
    /// <see cref="WireEvent{TPacked}"/> — from this type. <see cref="WriteToNetwork"/> and
    /// <see cref="ReadFromNetwork"/> are the bridge to Unity NGO's buffer primitives; the
    /// generic intermediate implements them via AutoPacker's typed accessors so leaves only
    /// declare <c>[Packed]</c> fields.
    /// </summary>
    [AutoPack]
    public abstract partial class WireEvent
    {
        /// <summary>Size in bytes of the leaf's <c>Packed</c> struct on the wire (discriminator excluded).</summary>
        public abstract int PackedSize { get; }

        /// <summary>Serialize this event's payload bytes into <paramref name="writer"/>.</summary>
        public abstract void WriteToNetwork(ref FastBufferWriter writer, IPackingHandler handler);

        /// <summary>Deserialize this event's payload bytes from <paramref name="reader"/> into the current instance.</summary>
        public abstract void ReadFromNetwork(ref FastBufferReader reader, IPackingHandler handler);
    }
}
