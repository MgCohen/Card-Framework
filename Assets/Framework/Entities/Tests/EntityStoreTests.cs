using System.Collections.Generic;
using NUnit.Framework;

namespace Framework.Entities.Tests
{
    public class EntityStoreTests
    {
        [Test]
        public void Create_AllocatesIdStartingAt1()
        {
            var store = new EntityStore();
            var entity = store.Create(EntityTypes.Card);
            Assert.AreEqual(1, entity.Id);
        }

        [Test]
        public void Create_IncrementsId()
        {
            var store = new EntityStore();
            var a = store.Create(EntityTypes.Card);
            var b = store.Create(EntityTypes.Card);
            var c = store.Create(EntityTypes.Card);

            Assert.AreEqual(1, a.Id);
            Assert.AreEqual(2, b.Id);
            Assert.AreEqual(3, c.Id);
        }

        [Test]
        public void Create_SetsTypeAndDefinitionId()
        {
            var store = new EntityStore();
            var entity = store.Create(EntityTypes.Card, 201);

            Assert.AreEqual(EntityTypes.Card, entity.Type);
            Assert.AreEqual(201, entity.DefinitionId);
        }

        [Test]
        public void Create_DefinitionIdDefaultsToZero()
        {
            var store = new EntityStore();
            var entity = store.Create(EntityTypes.Zone);
            Assert.AreEqual(0, entity.DefinitionId);
        }

        [Test]
        public void ById_ReturnsCreatedEntity()
        {
            var store = new EntityStore();
            var entity = store.Create(EntityTypes.Card);
            Assert.AreSame(entity, store.ById(entity.Id));
        }

        [Test]
        public void ById_UnknownId_Throws()
        {
            var store = new EntityStore();
            Assert.Throws<KeyNotFoundException>(() => store.ById(999));
        }

        [Test]
        public void TryGetById_ReturnsTrueForKnown()
        {
            var store = new EntityStore();
            var entity = store.Create(EntityTypes.Card);
            Assert.IsTrue(store.TryGetById(entity.Id, out var fetched));
            Assert.AreSame(entity, fetched);
        }

        [Test]
        public void TryGetById_ReturnsFalseForUnknown()
        {
            var store = new EntityStore();
            Assert.IsFalse(store.TryGetById(999, out _));
        }

        [Test]
        public void From_CreatesEntityWithDefIdAndType()
        {
            var store = new EntityStore();
            var def = new CardTestDef(101, 3);

            var entity = store.From(def);
            Assert.AreEqual(EntityTypes.Card, entity.Type);
            Assert.AreEqual(101, entity.DefinitionId);
        }

        [Test]
        public void From_AllocatesSequentialIds()
        {
            var store = new EntityStore();
            var a = store.From(new CardTestDef(101, 3));
            var b = store.From(new CardTestDef(102, 5));

            Assert.AreEqual(1, a.Id);
            Assert.AreEqual(2, b.Id);
        }
    }
}
