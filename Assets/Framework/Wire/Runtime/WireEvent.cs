using Scaffold.AutoPacker;
using Unity.Netcode;

namespace Framework.Wire
{
    [AutoPack]
    public abstract partial class WireEvent
    {
        public abstract int PackedSize { get; }
        public abstract void WriteToNetwork(ref FastBufferWriter writer, IPackingHandler handler);
        public abstract void ReadFromNetwork(ref FastBufferReader reader, IPackingHandler handler);
    }
}
