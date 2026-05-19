using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public partial class EntityCreatedEvent : WireEvent<EntityCreatedEvent.Packed>
    {
        [Packed] public long EntityId;
    }
}
