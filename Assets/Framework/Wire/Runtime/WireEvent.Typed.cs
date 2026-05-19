using Scaffold.AutoPacker;
using Unity.Netcode;

namespace Framework.Wire
{
    // Uses the unmanaged byte-pointer path (not FastBufferWriter.WriteValueSafe<T, ForStructs>)
    // because that overload requires INetworkSerializeByMemcpy — a marker we keep off
    // IPackedStruct to avoid coupling AutoPacker to NGO.
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
