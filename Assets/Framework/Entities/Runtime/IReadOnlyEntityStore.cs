namespace Framework.Entities
{
    public interface IReadOnlyEntityStore
    {
        Entity ById(int id);
        bool TryGetById(int id, out Entity entity);
    }
}
