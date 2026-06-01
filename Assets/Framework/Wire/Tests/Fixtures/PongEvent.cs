using Scaffold.AutoPacker;

namespace Framework.Wire.Tests
{
    // Second synthetic transport fixture, distinct type from PingEvent — used where a test
    // needs two different event types (channel-id ordering, multi-type stream ordering).
    public partial class PongEvent : WireEvent
    {
        [Packed] public int Value;
    }
}
