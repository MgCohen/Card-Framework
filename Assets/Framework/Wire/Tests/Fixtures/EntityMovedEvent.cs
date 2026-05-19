using Scaffold.AutoPacker;

namespace Framework.Wire.Tests
{
    public partial class EntityMovedEvent : WireEvent
    {
        [Packed] public long EntityId;
        [Packed] public Vec3 Position;
    }
}
