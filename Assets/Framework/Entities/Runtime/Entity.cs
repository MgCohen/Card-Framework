namespace Framework.Entities
{
    // Data only. Tag reads/writes go through TagService (server) or TagStore (client) by id.
    // No back-ref to either — keeps Entity registrable on both sides without dragging the
    // server-only TagService into the client scope.
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
