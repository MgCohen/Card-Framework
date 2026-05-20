using Framework.Tags;
using NUnit.Framework;

namespace Framework.Entities.Tests
{
    public class EntityTests
    {
        [Test]
        public void Construction_SetsIdTypeAndDefinitionId()
        {
            var store = new EntityStore();
            var entity = store.Create(EntityTypes.Card, 101);

            Assert.AreEqual(1, entity.Id);
            Assert.AreEqual(EntityTypes.Card, entity.Type);
            Assert.AreEqual(101, entity.DefinitionId);
        }

        [Test]
        public void PatternA_SetGetRoundTrip()
        {
            var store = new EntityStore();
            var entity = store.Create(EntityTypes.Card);
            var health = new TagKey<int>(9200);

            entity.Set(health, 30);
            Assert.AreEqual(30, entity.Get(health));
        }

        [Test]
        public void PatternA_HasAndRemove()
        {
            var store = new EntityStore();
            var entity = store.Create(EntityTypes.Card);
            var health = new TagKey<int>(9201);

            Assert.IsFalse(entity.Has(health));
            entity.Set(health, 5);
            Assert.IsTrue(entity.Has(health));
            Assert.IsTrue(entity.Remove(health));
            Assert.IsFalse(entity.Has(health));
            Assert.IsFalse(entity.Remove(health), "Second remove is a no-op");
        }

        [Test]
        public void PatternA_MultipleKeys_AreIndependent()
        {
            var store = new EntityStore();
            var entity = store.Create(EntityTypes.Card);

            entity.Set(SampleTags.Health, 30);
            entity.Set(SampleTags.Attack, 7);
            entity.Set(SampleTags.IsCard, true);

            Assert.AreEqual(30,   entity.Get(SampleTags.Health));
            Assert.AreEqual(7,    entity.Get(SampleTags.Attack));
            Assert.AreEqual(true, entity.Get(SampleTags.IsCard));
        }

        [Test]
        public void PatternB_ExtensionForm_RoundTrips()
        {
            var store = new EntityStore();
            var entity = store.Create(EntityTypes.Card);

            entity.Set<DeathrattleTestTag, bool>(true);
            Assert.IsTrue(entity.Get<DeathrattleTestTag, bool>());
            Assert.IsTrue(entity.Has<DeathrattleTestTag>());

            entity.Remove<DeathrattleTestTag>();
            Assert.IsFalse(entity.Has<DeathrattleTestTag>());
        }

        [Test]
        public void PatternB_NamedFieldForm_RoundTrips()
        {
            var store = new EntityStore();
            var entity = store.Create(EntityTypes.Card);

            entity.Set(Tag.Deathrattle, true);
            Assert.IsTrue(entity.Get(Tag.Deathrattle));
            Assert.IsTrue(entity.Has(Tag.Deathrattle));

            entity.Remove(Tag.Deathrattle);
            Assert.IsFalse(entity.Has(Tag.Deathrattle));
        }

        [Test]
        public void PatternB_BothForms_ResolveToSameSlot()
        {
            var store = new EntityStore();
            var entity = store.Create(EntityTypes.Card);

            entity.Set<DeathrattleTestTag, bool>(true);
            Assert.IsTrue(entity.Get(Tag.Deathrattle));

            entity.Set(Tag.Deathrattle, false);
            Assert.IsFalse(entity.Get<DeathrattleTestTag, bool>());
        }

        [Test]
        public void PatternB_TypedValue_NotJustBool()
        {
            var store = new EntityStore();
            var entity = store.Create(EntityTypes.Card);

            entity.Set<PoisonTestTag, int>(3);
            Assert.AreEqual(3, entity.Get<PoisonTestTag, int>());

            entity.Set(Tag.Poison, 7);
            Assert.AreEqual(7, entity.Get(Tag.Poison));
        }

        [Test]
        public void ToString_ContainsId()
        {
            var store = new EntityStore();
            var entity = store.Create(EntityTypes.Card);
            Assert.AreEqual("Entity#1", entity.ToString());
        }
    }
}
