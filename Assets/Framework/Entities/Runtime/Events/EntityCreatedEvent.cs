using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.Entities
{
    public partial class EntityCreatedEvent : WireEvent
    {
        [Packed] public int EntityId;
        [Packed] public int TypeSlot;
        [Packed] public int DefinitionId;
    }
}
