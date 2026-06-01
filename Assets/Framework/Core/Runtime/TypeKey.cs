namespace Framework.Core
{
    // Shared base for a Registry's members — the authored "kinds" a discriminator maps to.
    // Identity is (record type, Slot). The wire carries only the int Slot; receivers rehydrate
    // via SlotRegistry.BySlot. Concrete sets derive their own base over this (EntityType, Tag,
    // GameState) and declare members as records, so `is`/record-equality and Of<T>() all work.
    public abstract record TypeKey(int Slot);
}
