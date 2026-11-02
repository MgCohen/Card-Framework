using Framework.RecordIds;

namespace Framework.Blocks.Tests
{
    public sealed class TestEntity : IEntity
    {
        public TestEntity(int id) => Id = new EntityId { Value = id };

        public EntityId Id { get; }
    }
}
