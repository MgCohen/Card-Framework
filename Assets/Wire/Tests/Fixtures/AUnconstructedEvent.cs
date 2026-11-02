using Scaffold.AutoPacker;

namespace Framework.Wire.Tests
{
    // No code constructs this event. Its name sorts before PingEvent, so it takes a channel id before it.
    public partial class AUnconstructedEvent : WireEvent
    {
        [Packed] public int Value;
    }
}
