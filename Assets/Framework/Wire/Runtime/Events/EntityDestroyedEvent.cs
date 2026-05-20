using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public partial class EntityDestroyedEvent : WireEvent<EntityDestroyedEvent.Packed>
    {
        [Packed] public int EntityId;
    }
}
