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
        public void PatternB_TypeParameterForm_RoundTrips()
        {
            // bag.Set<TTag>(value) — type-param sugar. No cast, no key reference at the call site.
            var bag = new TagBagProbe();

            bag.Set<SampleDeathrattle>(true);
            Assert.IsTrue(bag.Get<SampleDeathrattle, bool>());
            Assert.IsTrue(bag.Has<SampleDeathrattle>());

            bag.Set<SampleDeathrattle>(false);
            Assert.IsFalse(bag.Get<SampleDeathrattle, bool>(), "false is a valid value — silenced, not absent");
            Assert.IsTrue(bag.Has<SampleDeathrattle>(),       "Has stays true while slot is occupied");

            bag.Remove<SampleDeathrattle>();
            Assert.IsFalse(bag.Has<SampleDeathrattle>());
        }

        [Test]
        public void PatternB_NamedFieldForm_RoundTrips()
        {
            // bag.Set(Tag.SampleDeathrattle, value) — named-field accessor. Same underlying
            // singleton as Tag<X, T>.Key; the named-field surface is declared once per assembly.
            var bag = new TagBagProbe();

            bag.Set(Tag.SampleDeathrattle, true);
            Assert.IsTrue(bag.Get(Tag.SampleDeathrattle));

            bag.Set(Tag.SampleDeathrattle, false);
            Assert.IsFalse(bag.Get(Tag.SampleDeathrattle), "false is a valid value — silenced, not absent");
            Assert.IsTrue(bag.Has(Tag.SampleDeathrattle),  "Has stays true while slot is occupied");

            bag.Remove(Tag.SampleDeathrattle);
            Assert.IsFalse(bag.Has(Tag.SampleDeathrattle));
        }

        [Test]
        public void PatternB_BothForms_ResolveToSameKey()
        {
            // Type-param and named-field forms must agree — both writes mutate the same slot.
            var bag = new TagBagProbe();

            bag.Set<SampleDeathrattle>(true);
            Assert.IsTrue(bag.Get(Tag.SampleDeathrattle), "Named-field read sees the type-param write");

            bag.Set(Tag.SampleDeathrattle, false);
            Assert.IsFalse(bag.Get<SampleDeathrattle, bool>(), "Type-param read sees the named-field write");
        }

        [Test]
        public void PatternB_TypedValue_NotJustBool()
        {
            // SamplePoison : ITagKey<int> — same clean call sites with a non-bool value type.
            var bag = new TagBagProbe();

            bag.Set<SamplePoison>(3);
            Assert.AreEqual(3, bag.Get<SamplePoison, int>());

            bag.Set(Tag.SamplePoison, 7);
            Assert.AreEqual(7, bag.Get(Tag.SamplePoison));
        }
    }
}
