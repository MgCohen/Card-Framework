using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public partial class EntityCreatedEvent : WireEvent
    {
        [Packed] public long EntityId;
    }
}
