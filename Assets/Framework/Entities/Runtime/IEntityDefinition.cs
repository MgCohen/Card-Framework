namespace Framework.Entities
{
    public interface IEntityDefinition
    {
        int Id { get; }
        EntityType Type { get; }
    }
}
