using System.Collections.Generic;
using NUnit.Framework;

namespace Framework.Entities.Tests
{
    // The store is explicit-id only — id allocation lives on EntityService (see
    // EntityServiceTests). These cover the store's storage contract: idempotent insert + reads.
    public class EntityStoreTests
    {
        [Test]
        public void Insert_StoresWithGivenId()
        {
            var store = new TestStack().EntityStore;
            var entity = store.Insert(42, new Card(), 101);

            Assert.AreEqual(42, entity.Id);
            Assert.AreSame(entity, store.ById(42));
        }

        [Test]
        public void Insert_SetsTypeAndDefinitionId()
        {
            var store = new TestStack().EntityStore;
            var entity = store.Insert(1, new Card(), 201);

            Assert.AreEqual(new Card(), entity.Type);
            Assert.AreEqual(201, entity.DefinitionId);
        }

        [Test]
        public void Insert_DuplicateId_ReturnsExistingInstance()
        {
            // Apply-handler path: replaying EntityCreatedEvent must be a no-op rather than throw.
            var store = new TestStack().EntityStore;
            var first  = store.Insert(42, new Card(), 101);
            var second = store.Insert(42, new Card(), 999); // ignored — entity already exists

            Assert.AreSame(first, second);
            Assert.AreEqual(101, second.DefinitionId, "DefId stays at the first-insert value");
        }

        [Test]
        public void ById_ReturnsInsertedEntity()
        {
            var store = new TestStack().EntityStore;
            var entity = store.Insert(1, new Card(), 0);
            Assert.AreSame(entity, store.ById(entity.Id));
        }

        [Test]
        public void ById_UnknownId_Throws()
        {
            var store = new TestStack().EntityStore;
            Assert.Throws<KeyNotFoundException>(() => store.ById(999));
        }

        [Test]
        public void TryGetById_ReturnsTrueForKnown()
        {
            var store = new TestStack().EntityStore;
            var entity = store.Insert(1, new Card(), 0);
            Assert.IsTrue(store.TryGetById(entity.Id, out var fetched));
            Assert.AreSame(entity, fetched);
        }

        [Test]
        public void TryGetById_ReturnsFalseForUnknown()
        {
            var store = new TestStack().EntityStore;
            Assert.IsFalse(store.TryGetById(999, out _));
        }
    }
}
