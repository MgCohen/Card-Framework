using System;
using NUnit.Framework;

namespace Framework.Tags.Tests
{
    public class RegistrationTests
    {
        [Test]
        public void PatternA_RegistersOnFirstTouch()
        {
            var key = SampleTags.Health;

            Assert.AreEqual(1001, key.Slot);
            Assert.AreEqual(typeof(int), key.ValueType);
            Assert.AreSame(key, TagRegistry.BySlot(1001));
        }

        [Test]
        public void PatternB_SynthesizesTypedKeyAtDeclaredSlot()
        {
            var key = Tag<SampleDeathrattle>.Key;

            Assert.AreEqual(1100, key.Slot);
            Assert.AreEqual(typeof(bool), key.ValueType);
            Assert.AreSame(key, TagRegistry.BySlot(1100));
        }

        [Test]
        public void PatternB_HonoursDeclaredValueType()
        {
            var key = Tag<SamplePoison>.Key;

            Assert.AreEqual(1101, key.Slot);
            Assert.AreEqual(typeof(int), key.ValueType);
        }

        [Test]
        public void PatternB_AccessIsIdempotent()
        {
            var first  = Tag<SampleDeathrattle>.Key;
            var second = Tag<SampleDeathrattle>.Key;
            Assert.AreSame(first, second);
        }

        [Test]
        public void Duplicate_Slot_Throws()
        {
            _ = SampleTags.Health;

            Assert.Throws<InvalidOperationException>(
                () => new TagKey<int>(1001));
        }
    }
}
