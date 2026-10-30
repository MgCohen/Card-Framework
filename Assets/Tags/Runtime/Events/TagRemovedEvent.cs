using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Tags
{
    public partial class TagRemovedEvent : WireEvent
    {
        [Packed] public int EntityId;
        [Packed] public int Slot;
    }
}
