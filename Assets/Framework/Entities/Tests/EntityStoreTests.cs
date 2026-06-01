using System.Collections.Generic;
using NUnit.Framework;

namespace Framework.Entities.Tests
{
    public class EntityStoreTests
    {
        [Test]
        public void Insert_AllocatesIdStartingAt1()
        {
            var store = new TestStack().EntityStore;
            var entity = store.Insert(new Card());
            Assert.AreEqual(1, entity.Id);
        }

        [Test]
        public void Insert_IncrementsId()
        {
            var store = new TestStack().EntityStore;
            var a = store.Insert(new Card());
            var b = store.Insert(new Card());
            var c = store.Insert(new Card());

            Assert.AreEqual(1, a.Id);
            Assert.AreEqual(2, b.Id);
            Assert.AreEqual(3, c.Id);
        }

        [Test]
        public void Insert_SetsTypeAndDefinitionId()
        {
            var store = new TestStack().EntityStore;
            var entity = store.Insert(new Card(), 201);

            Assert.AreEqual(new Card(), entity.Type);
            Assert.AreEqual(201, entity.DefinitionId);
        }

        [Test]
        public void Insert_DefinitionIdDefaultsToZero()
        {
            var store = new TestStack().EntityStore;
            var entity = store.Insert(new Zone());
            Assert.AreEqual(0, entity.DefinitionId);
        }

        [Test]
        public void ById_ReturnsInsertedEntity()
        {
            var store = new TestStack().EntityStore;
            var entity = store.Insert(new Card());
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
            var entity = store.Insert(new Card());
            Assert.IsTrue(store.TryGetById(entity.Id, out var fetched));
            Assert.AreSame(entity, fetched);
        }

        [Test]
        public void TryGetById_ReturnsFalseForUnknown()
        {
            var store = new TestStack().EntityStore;
            Assert.IsFalse(store.TryGetById(999, out _));
        }

        [Test]
        public void From_CreatesEntityWithDefIdAndType()
        {
            var store = new TestStack().EntityStore;
            var def = new CardTestDef(101, 3);

            var entity = store.From(def);
            Assert.AreEqual(new Card(), entity.Type);
            Assert.AreEqual(101, entity.DefinitionId);
        }

        [Test]
        public void From_AllocatesSequentialIds()
        {
            var store = new TestStack().EntityStore;
            var a = store.From(new CardTestDef(101, 3));
            var b = store.From(new CardTestDef(102, 5));

            Assert.AreEqual(1, a.Id);
            Assert.AreEqual(2, b.Id);
        }

        [Test]
        public void Insert_WithPresetId_UsesThatId()
        {
            var store = new TestStack().EntityStore;
            var entity = store.Insert(42, new Card(), 101);

            Assert.AreEqual(42, entity.Id);
            Assert.AreSame(entity, store.ById(42));
        }

        [Test]
        public void Insert_WithPresetId_AdvancesNextIdPastIt()
        {
            var store = new TestStack().EntityStore;
            store.Insert(42, new Card(), 101);
            var next = store.Insert(new Card());

            Assert.AreEqual(43, next.Id);
        }

        [Test]
        public void Insert_WithPresetId_DuplicateReturnsExistingInstance()
        {
            // Apply-handler path: replaying EntityCreatedEvent must be a no-op rather than throw.
            var store = new TestStack().EntityStore;
            var first  = store.Insert(42, new Card(), 101);
            var second = store.Insert(42, new Card(), 999); // ignored — entity already exists

            Assert.AreSame(first, second);
            Assert.AreEqual(101, second.DefinitionId, "DefId stays at the first-insert value");
        }
    }
}
