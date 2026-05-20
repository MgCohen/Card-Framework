using NUnit.Framework;

namespace Framework.Entities.Tests
{
    public class SetGetTests
    {
        [Test]
        public void PatternA_SetThenGet_RoundTrips()
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
        public void PatternA_Has_BeforeAndAfterSet()
        {
            var store = new EntityStore();
            var entity = store.Create(EntityTypes.Card);

            Assert.IsFalse(entity.Has(SampleTags.Health));
            entity.Set(SampleTags.Health, 1);
            Assert.IsTrue(entity.Has(SampleTags.Health));
        }

        [Test]
        public void PatternA_Remove_ClearsSlot()
        {
            var store = new EntityStore();
            var entity = store.Create(EntityTypes.Card);

            entity.Set(SampleTags.Health, 5);
            Assert.IsTrue(entity.Remove(SampleTags.Health));
            Assert.IsFalse(entity.Has(SampleTags.Health));
            Assert.IsFalse(entity.Remove(SampleTags.Health), "Second remove is a no-op");
        }

        [Test]
        public void PatternB_TypeParameterForm_RoundTrips()
        {
            var store = new EntityStore();
            var entity = store.Create(EntityTypes.Card);

            entity.Set<SampleDeathrattle, bool>(true);
            Assert.IsTrue(entity.Get<SampleDeathrattle, bool>());
            Assert.IsTrue(entity.Has<SampleDeathrattle>());

            entity.Set<SampleDeathrattle, bool>(false);
            Assert.IsFalse(entity.Get<SampleDeathrattle, bool>(), "false is a valid value — silenced, not absent");
            Assert.IsTrue(entity.Has<SampleDeathrattle>(),        "Has stays true while slot is occupied");

            entity.Remove<SampleDeathrattle>();
            Assert.IsFalse(entity.Has<SampleDeathrattle>());
        }

        [Test]
        public void PatternB_NamedFieldForm_RoundTrips()
        {
            var store = new EntityStore();
            var entity = store.Create(EntityTypes.Card);

            entity.Set(Tag.SampleDeathrattle, true);
            Assert.IsTrue(entity.Get(Tag.SampleDeathrattle));

            entity.Set(Tag.SampleDeathrattle, false);
            Assert.IsFalse(entity.Get(Tag.SampleDeathrattle), "false is a valid value — silenced, not absent");
            Assert.IsTrue(entity.Has(Tag.SampleDeathrattle),  "Has stays true while slot is occupied");

            entity.Remove(Tag.SampleDeathrattle);
            Assert.IsFalse(entity.Has(Tag.SampleDeathrattle));
        }

        [Test]
        public void PatternB_BothForms_ResolveToSameKey()
        {
            var store = new EntityStore();
            var entity = store.Create(EntityTypes.Card);

            entity.Set<SampleDeathrattle, bool>(true);
            Assert.IsTrue(entity.Get(Tag.SampleDeathrattle), "Named-field read sees the type-param write");

            entity.Set(Tag.SampleDeathrattle, false);
            Assert.IsFalse(entity.Get<SampleDeathrattle, bool>(), "Type-param read sees the named-field write");
        }

        [Test]
        public void PatternB_TypedValue_NotJustBool()
        {
            var store = new EntityStore();
            var entity = store.Create(EntityTypes.Card);

            entity.Set<SamplePoison, int>(3);
            Assert.AreEqual(3, entity.Get<SamplePoison, int>());

            entity.Set(Tag.SamplePoison, 7);
            Assert.AreEqual(7, entity.Get(Tag.SamplePoison));
        }
    }
}
