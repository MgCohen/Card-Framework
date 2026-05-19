using System;
using NUnit.Framework;

namespace Framework.Tags.Tests
{
    // Pattern A — static-instance tags. The base ctor calls TagRegistry.RegisterInstance.
    static class SampleTags
    {
        public static readonly TagKey<int>  Health = new(1001);
        public static readonly TagKey<int>  Attack = new(1002);
        public static readonly TagKey<bool> IsCard = new(1003);
    }

    // Pattern B — dual-role class. Carries a payload (Source); also serves as the tag handle.
    [TagSlot(1100)]
    sealed class SampleDeathrattle : ITagKey<bool>
    {
        public int SourceEntityId { get; }
        public SampleDeathrattle(int sourceEntityId) { SourceEntityId = sourceEntityId; }
    }

    // Pattern B with non-bool value-type — synthesises TagKey<int>.
    [TagSlot(1101)]
    sealed class SamplePoison : ITagKey<int>
    {
        public int Source { get; }
        public int Amount { get; }
        public SamplePoison(int source, int amount) { Source = source; Amount = amount; }
    }

    public class RegistrationTests
    {
        [Test]
        public void PatternA_RegistersOnFirstTouch()
        {
            // Touch the static field — base ctor fires Register.
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
            // Force Health to be registered first.
            _ = SampleTags.Health;

            Assert.Throws<InvalidOperationException>(
                () => new TagKey<int>(1001));
        }
    }
}
