using Scaffold.AutoPacker;

namespace Framework.Wire.Tests
{
    public partial class EntityLabeledEvent : WireEvent
    {
        [Packed] public long EntityId;
        [Packed(typeof(int))] public string Label;
    }
}
