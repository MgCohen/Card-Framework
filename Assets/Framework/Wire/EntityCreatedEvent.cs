using Scaffold.AutoPacker;

namespace Framework.Wire
{
    [AutoPack]
    public partial class EntityCreatedEvent : WireEvent
    {
        [Packed] public long EntityId;
    }
}
