using Framework.Wire;
using Scaffold.AutoPacker;

namespace Framework.States
{
    // Transition event — one slot-discriminated channel for all states (replaces the per-type
    // StatePushEvent<TState>, which spent one channel + handler per state). The wire carries the
    // int Slot; the receiver rehydrates the cursor via GameStateRegistry.BySlot.
    public partial class StatePushEvent : WireEvent
    {
        [Packed] public int Slot;
    }
}
