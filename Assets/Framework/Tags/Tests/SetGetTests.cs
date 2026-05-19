using NUnit.Framework;

namespace Framework.Tags.Tests
{
    // Uses TagBagProbe (TEST STUB) to exercise Set/Get/Has/Remove against real TagKeys.
    public class SetGetTests
    {
        [Test]
        public void PatternA_SetThenGet_RoundTrips()
        {
            var bag = new TagBagProbe();

            bag.Set(SampleTags.Health, 30);
            bag.Set(SampleTags.Attack, 7);
            bag.Set(SampleTags.IsCard, true);

            Assert.AreEqual(30,   bag.Get(SampleTags.Health));
            Assert.AreEqual(7,    bag.Get(SampleTags.Attack));
            Assert.AreEqual(true, bag.Get(SampleTags.IsCard));
        }

        [Test]
        public void PatternA_Has_BeforeAndAfterSet()
        {
            var bag = new TagBagProbe();

            Assert.IsFalse(bag.Has(SampleTags.Health));
            bag.Set(SampleTags.Health, 1);
            Assert.IsTrue(bag.Has(SampleTags.Health));
        }

        [Test]
        public void PatternA_Remove_ClearsSlot()
        {
            var bag = new TagBagProbe();

            bag.Set(SampleTags.Health, 5);
            Assert.IsTrue(bag.Remove(SampleTags.Health));
            Assert.IsFalse(bag.Has(SampleTags.Health));
            Assert.IsFalse(bag.Remove(SampleTags.Health), "Second remove is a no-op");
        }

        [Test]
        public void PatternB_KeyUsableWithSetGet_AfterCast()
        {
            // Tag<TClass>.Key returns base TagKey — caller has to cast to the
            // typed key to use the typed Set/Get. Documents the ergonomic gap
            // the future Entity API overloads (Set<TTag>(T) where TTag : ITagKey<T>)
            // will close.
            var bag = new TagBagProbe();
            var deathrattle = (TagKey<bool>)Tag<SampleDeathrattle>.Key;

            bag.Set(deathrattle, true);
            Assert.IsTrue(bag.Get(deathrattle));

            bag.Set(deathrattle, false);
            Assert.IsFalse(bag.Get(deathrattle), "false is a valid value — silenced, not absent");
            Assert.IsTrue(bag.Has(deathrattle),  "Has stays true while slot is occupied");

            bag.Remove(deathrattle);
            Assert.IsFalse(bag.Has(deathrattle));
        }

        [Test]
        public void PatternB_TypedValue_NotJustBool()
        {
            var bag = new TagBagProbe();
            var poison = (TagKey<int>)Tag<SamplePoison>.Key;

            bag.Set(poison, 3);
            Assert.AreEqual(3, bag.Get(poison));
        }
    }
}
