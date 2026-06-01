using Framework.Core;

namespace Framework.Entities
{
    // A kind of entity — a Registry member over TypeKey. Identity is (record type, Slot); the
    // wire carries only the int Slot and receivers rehydrate via EntityTypeRegistry.BySlot.
    // Games author additional kinds as records of their own and register them in a game layer.
    public abstract record EntityType(int Slot) : TypeKey(Slot);

    // Framework baselines. Slot numbers preserved from the old static table; slot 1 is
    // intentionally reserved (not pre-registered).
    public sealed record Unknown()  : EntityType(0);
    public sealed record Player()   : EntityType(2);
    public sealed record Zone()     : EntityType(3);
    public sealed record Card()     : EntityType(4);
    public sealed record Modifier() : EntityType(5);
}
