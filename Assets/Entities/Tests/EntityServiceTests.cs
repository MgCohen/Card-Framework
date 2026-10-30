using NUnit.Framework;

namespace Framework.Entities.Tests
{
    // EntityService owns id allocation (the store is explicit-id only). Create allocates,
    // emits EntityCreatedEvent, and returns the entity the handler synchronously inserted.
    public class EntityServiceTests
    {
        [Test]
        public void Create_AllocatesIdStartingAtOne()
        {
            var entities = new TestStack().Entities;
            Assert.AreEqual(1, entities.Create(new Card()).Id);
        }

        [Test]
        public void Create_AllocatesSequentialIds()
        {
            var entities = new TestStack().Entities;
            Assert.AreEqual(1, entities.Create(new Card()).Id);
            Assert.AreEqual(2, entities.Create(new Card()).Id);
            Assert.AreEqual(3, entities.Create(new Zone()).Id);
        }

        [Test]
        public void Create_SetsTypeAndDefinitionId()
        {
            var entities = new TestStack().Entities;
            var entity = entities.Create(new Card(), 201);

            Assert.AreEqual(new Card(), entity.Type);
            Assert.AreEqual(201, entity.DefinitionId);
        }

        [Test]
        public void Create_DefinitionIdDefaultsToZero()
        {
            var entities = new TestStack().Entities;
            Assert.AreEqual(0, entities.Create(new Zone()).DefinitionId);
        }

        [Test]
        public void Create_ReturnsEntityInStore()
        {
            var stack = new TestStack();
            var created = stack.Entities.Create(new Card());
            Assert.AreSame(created, stack.EntityStore.ById(created.Id));
        }
    }
}
