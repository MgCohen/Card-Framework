namespace Framework.Tags
{
    // Read-only view over TagStore. GameEngine exposes this, not the concrete store, so
    // consumers query but never write — writes go through TagService → handler.
    public interface IReadOnlyTagStore
    {
        T Get<T>(int entityId, int slot) where T : unmanaged;
        bool Has(int entityId, int slot);
    }
}
