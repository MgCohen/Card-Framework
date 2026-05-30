using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Tags
{
    // Emitted by TagService.Set when overwriting an existing value.
    // Carries both Prev and Next so presentation/animation consumers can react to the delta.
    public partial class TagChangeEvent<T> : WireEvent where T : unmanaged
    {
        [Packed] public int EntityId;
        [Packed] public int Slot;
        [Packed] public T   Prev;
        [Packed] public T   Next;
    }
}
