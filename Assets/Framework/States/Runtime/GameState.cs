using Framework.Core;

namespace Framework.States
{
    // A position in the game's FSM ("Playing", "Combat", "Mulligan") — a Registry member over
    // TypeKey. Identity is (record type, Slot); the wire carries only the int Slot and the
    // receiver rehydrates the cursor via GameStateRegistry.BySlot. Zero data beyond the slot for
    // now; the behaviour surface (Actions / OnEnter / OnExit) lands in 5c on the record itself.
    //
    // Phase-structured games author additional GameState records of their own and register them
    // in a game layer. Baselines live one-per-file alongside.
    public abstract record GameState(int Slot) : TypeKey(Slot);
}
