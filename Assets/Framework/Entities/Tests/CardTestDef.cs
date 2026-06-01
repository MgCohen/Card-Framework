namespace Framework.Entities.Tests
{
    public sealed record CardTestDef(int Id, int Cost) : IEntityDefinition
    {
        public EntityType Type => new Card();
    }
}
