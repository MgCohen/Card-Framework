using System;
using NUnit.Framework;

namespace Framework.Tags.Tests
{
    static class RegSampleTags
    {
        public static readonly TagKey<int> Health = new(2001);
    }

    [TagSlot(2100)] sealed class RegSampleDeathrattle : ITagKey<bool> { }
    [TagSlot(2101)] sealed class RegSamplePoison     : ITagKey<int>  { }

    public class RegistrationTests
    {
        [Test]
        public void PatternA_RegistersOnFirstTouch()
        {
            var key = RegSampleTags.Health;

            Assert.AreEqual(2001, key.Slot);
            Assert.AreEqual(typeof(int), key.ValueType);
            Assert.AreSame(key, TagRegistry.BySlot(2001));
        }

        [Test]
        public void PatternB_SynthesizesTypedKeyAtDeclaredSlot()
        {
            var key = Tag<RegSampleDeathrattle>.Key;

            Assert.AreEqual(2100, key.Slot);
            Assert.AreEqual(typeof(bool), key.ValueType);
            Assert.AreSame(key, TagRegistry.BySlot(2100));
        }

        [Test]
        public void PatternB_HonoursDeclaredValueType()
        {
            var key = Tag<RegSamplePoison>.Key;

            Assert.AreEqual(2101, key.Slot);
            Assert.AreEqual(typeof(int), key.ValueType);
        }

        [Test]
        public void PatternB_AccessIsIdempotent()
        {
            var first  = Tag<RegSampleDeathrattle>.Key;
            var second = Tag<RegSampleDeathrattle>.Key;
            Assert.AreSame(first, second);
        }

        [Test]
        public void Duplicate_Slot_Throws()
        {
            _ = RegSampleTags.Health;

            Assert.Throws<InvalidOperationException>(
                () => new TagKey<int>(2001));
        }
    }
}
