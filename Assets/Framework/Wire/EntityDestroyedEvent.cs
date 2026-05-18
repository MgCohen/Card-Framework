using Scaffold.AutoPacker;

namespace Framework.Wire
{
    [AutoPack]
    public partial class EntityDestroyedEvent : WireEvent
    {
        [Packed] public long EntityId;
    }
}
