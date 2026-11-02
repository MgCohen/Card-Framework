using NUnit.Framework;

namespace Framework.RecordIds.Tests
{
    public class EntityIdTests
    {
        sealed class TestEntity : IEntity
        {
            public EntityId Id { get; init; }
        }

        [Test]
        public void EntityId_IsNotEqualToAnotherIdType()
        {
            Id entity = new EntityId { Value = 5 };
            Id other = new AId { Value = 5 };

            Assert.AreNotEqual(entity, other);
        }

        [Test]
        public void Entity_IsARecordKeyedByEntityId()
        {
            IRecord<EntityId> record = new TestEntity { Id = new EntityId { Value = 3 } };

            Assert.AreEqual(new EntityId { Value = 3 }, record.Id);
        }
    }
}
