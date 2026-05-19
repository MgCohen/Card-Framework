using Scaffold.AutoPacker;

namespace Framework.Wire.Tests
{
    public partial class CardPlayedEvent : WireEvent
    {
        [Packed] public long EntityId;
        [Packed(typeof(int))] public CardDefinition Definition;
    }
}
