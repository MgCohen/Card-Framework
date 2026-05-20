using Scaffold.AutoPacker;

namespace Framework.Wire
{
    public partial class EntityCreatedEvent : WireEvent<EntityCreatedEvent.Packed>
    {
        [Packed] public int EntityId;
        [Packed] public int TypeSlot;
        [Packed] public int DefinitionId;
    }
}
