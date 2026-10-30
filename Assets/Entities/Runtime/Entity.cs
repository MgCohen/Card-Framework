namespace Framework.Entities
{
    public sealed class Entity
    {
        public int        Id           { get; }
        public EntityType Type         { get; }
        public int        DefinitionId { get; }

        internal Entity(int id, EntityType type, int definitionId)
        {
            Id = id;
            Type = type;
            DefinitionId = definitionId;
        }

        public override string ToString() => $"Entity#{Id}";
    }
}
