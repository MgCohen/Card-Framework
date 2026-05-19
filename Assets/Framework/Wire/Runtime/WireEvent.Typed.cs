using Scaffold.AutoPacker;
using Unity.Netcode;

namespace Framework.Wire
{
    /// <summary>
    /// Generic intermediate base that bridges AutoPacker's typed accessors to NGO's
    /// <see cref="FastBufferWriter"/> / <see cref="FastBufferReader"/>. Concrete events
    /// inherit <c>WireEvent&lt;X.Packed&gt;</c> and gain zero-box wire serialization for free —
    /// AutoPacker emits the <c>PackTyped</c> / <c>UnpackTyped</c> overrides automatically.
    /// </summary>
    /// <remarks>
    /// Uses the unmanaged byte-pointer path on FastBufferWriter/Reader rather than
    /// <c>WriteValueSafe&lt;T, ForStructs&gt;</c>, because NGO's <c>ForStructs</c> overload requires
    /// <c>INetworkSerializeByMemcpy</c> — a marker we deliberately keep off
    /// <see cref="IPackedStruct"/> to avoid coupling AutoPacker to NGO. The unsafe block
    /// is the single point where this framework adapts AutoPacker's typed structs to NGO's
    /// constraints.
    /// </remarks>
    public abstract class WireEvent<TPacked> : WireEvent, IPackable<TPacked>, IUnpackable<TPacked>
        where TPacked : unmanaged, IPackedStruct
    {
        public sealed override unsafe int PackedSize => sizeof(TPacked);

        public abstract TPacked PackTyped(IPackingHandler handler = null);
        public abstract void UnpackTyped(in TPacked packed, IPackingHandler handler = null);

        public sealed override unsafe void WriteToNetwork(ref FastBufferWriter writer, IPackingHandler handler)
        {
            var packed = PackTyped(handler);
            writer.WriteBytesSafe((byte*)&packed, sizeof(TPacked));
        }

        public sealed override unsafe void ReadFromNetwork(ref FastBufferReader reader, IPackingHandler handler)
        {
            TPacked packed;
            reader.ReadBytesSafe((byte*)&packed, sizeof(TPacked));
            UnpackTyped(packed, handler);
        }
    }
}
