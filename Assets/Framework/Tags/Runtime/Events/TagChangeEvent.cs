using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Tags
{
    public partial class TagChangeEvent<T> : WireEvent where T : unmanaged
    {
        [Packed] public int EntityId;
        [Packed] public int Slot;
        [Packed] public T   Prev;
        [Packed] public T   Next;
    }
}
