using Scaffold.AutoPacker;

namespace Framework.Wire.Tests
{
    // Synthetic single-field transport fixture. Wire tests prove transport, not domain
    // events — PingEvent stands in for "some event with an int payload" so the round-trip,
    // ordering, and dispatch tests don't depend on any feature's domain event.
    public partial class PingEvent : WireEvent
    {
        [Packed] public int Value;
    }
}
