using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public partial class EntityDestroyedEvent : WireEvent
    {
        [Packed] public int EntityId;
    }
}
