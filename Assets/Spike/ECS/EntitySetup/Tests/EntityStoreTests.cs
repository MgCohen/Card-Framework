#nullable enable
using NUnit.Framework;

namespace Spike.EntitySetup.Tests
{
    public class EntityStoreTests
    {
        [Test]
        public void Create_AssignsMonotonicIds_StartingAt1()
        {
            var store = new EntityStore();
            var a = store.Create(EntityTypes.Card);
            var b = store.Create(EntityTypes.Card);
            var c = store.Create(EntityTypes.Zone);
            Assert.That(a.Id, Is.EqualTo(1));
            Assert.That(b.Id, Is.EqualTo(2));
            Assert.That(c.Id, Is.EqualTo(3));
        }

        [Test]
        public void Create_StampsTypeAndDefinitionId()
        {
            var store = new EntityStore();
            var e = store.Create(EntityTypes.Card, defId: 201);
            Assert.That(e.Type, Is.EqualTo(EntityTypes.Card));
            Assert.That(e.DefinitionId, Is.EqualTo(201));
        }

        [Test]
        public void Create_WithoutDefId_LeavesDefIdZero()
        {
            var store = new EntityStore();
            var e = store.Create(EntityTypes.Player);
            Assert.That(e.DefinitionId, Is.EqualTo(0));
        }

        [Test]
        public void Get_ReturnsCreatedEntity()
        {
            var store = new EntityStore();
            var e = store.Create(EntityTypes.Card);
            Assert.That(store.Get(e.Id), Is.SameAs(e));
        }

        [Test]
        public void Get_UnknownId_ReturnsNull()
        {
            var store = new EntityStore();
            Assert.That(store.Get(999), Is.Null);
        }

        [Test]
        public void Destroy_RemovesEntityFromStore()
        {
            var store = new EntityStore();
            var e = store.Create(EntityTypes.Card);
            store.Destroy(e.Id);
            Assert.That(store.Get(e.Id), Is.Null);
            Assert.That(store.All, Does.Not.ContainKey(e.Id));
        }

        [Test]
        public void All_EnumeratesEveryLiveEntity()
        {
            var store = new EntityStore();
            var a = store.Create(EntityTypes.Card);
            var b = store.Create(EntityTypes.Zone);
            Assert.That(store.All.Count, Is.EqualTo(2));
            Assert.That(store.All[a.Id], Is.SameAs(a));
            Assert.That(store.All[b.Id], Is.SameAs(b));
        }
    }
}
