namespace Framework.Entities
{
    // Read-only view over EntityStore. GameEngine exposes this, not the concrete store, so
    // consumers query but never write — writes go through EntityService → handler.
    public interface IReadOnlyEntityStore
    {
        Entity ById(int id);
        bool TryGetById(int id, out Entity entity);
    }
}
