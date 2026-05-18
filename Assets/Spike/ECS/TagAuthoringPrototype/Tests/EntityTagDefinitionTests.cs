#nullable enable
using NUnit.Framework;
using TagAuthoringPrototype.Authoring;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Tags;
using static TagAuthoringPrototype.Tests.TagTestSupport;

namespace TagAuthoringPrototype.Tests
{
    /// <summary>
    /// Pattern C call-site ergonomics: <c>entity.Set(tag, v)</c> /
    /// <c>Get(tag)</c> / <c>Has(tag)</c> / <c>Remove(tag)</c> read identically
    /// whether <c>tag</c> is a code-defined <see cref="TagKey{T}"/> (Pattern A)
    /// or a data-defined <see cref="TagDefinition{T}"/> (Pattern C). The
    /// extension methods in <c>EntityTagDefinitionExtensions</c> are what
    /// keep that surface uniform.
    /// </summary>
    public class EntityTagDefinitionTests
    {
        private static IntTag NewBakedIntTag(string id = "int") =>
            Bake(Make<IntTag>(NextSlot(), id));

        private static T Bake<T>(T tag) where T : TagDefinition
        {
            TagAuthoringRegistry.Bake(new TagDefinition[] { tag });
            return tag;
        }

        [Test]
        public void Set_ThenGet_RoundTripsValue()
        {
            var tag = NewBakedIntTag();
            var e = new Entity();
            e.Set(tag, 30);
            Assert.That(e.Get(tag), Is.EqualTo(30));
        }

        [Test]
        public void Get_BeforeSet_ReturnsDefault()
        {
            var tag = NewBakedIntTag();
            var e = new Entity();
            Assert.That(e.Get(tag), Is.EqualTo(0));
        }

        [Test]
        public void Has_TogglesWithSetAndRemove()
        {
            var tag = NewBakedIntTag();
            var e = new Entity();
            Assert.That(e.Has(tag), Is.False);
            e.Set(tag, 7);
            Assert.That(e.Has(tag), Is.True);
            e.Remove(tag);
            Assert.That(e.Has(tag), Is.False);
        }

        [Test]
        public void Set_Overwrite_KeepsLatestValue()
        {
            var tag = NewBakedIntTag();
            var e = new Entity();
            e.Set(tag, 1);
            e.Set(tag, 2);
            e.Set(tag, 3);
            Assert.That(e.Get(tag), Is.EqualTo(3));
        }

        [Test]
        public void Set_RoutesThroughSameSlotAsKey()
        {
            // The extension's job is just to forward to def.Key — verify by
            // setting through the def and reading through the underlying key
            // (and vice versa). Both must hit the same slot.
            var tag = NewBakedIntTag();
            var e = new Entity();

            e.Set(tag, 42);
            Assert.That(e.Get(tag.Key), Is.EqualTo(42));

            e.Set(tag.Key, 99);
            Assert.That(e.Get(tag), Is.EqualTo(99));
        }

        [Test]
        public void BoolTag_RoundTrips()
        {
            var tag = Bake(Make<BoolTag>(NextSlot(), "flag"));
            var e = new Entity();
            Assert.That(e.Get(tag), Is.False);
            e.Set(tag, true);
            Assert.That(e.Get(tag), Is.True);
        }

        [Test]
        public void StringTag_RoundTrips()
        {
            var tag = Bake(Make<StringTag>(NextSlot(), "element"));
            var e = new Entity();
            e.Set(tag, "Fire");
            Assert.That(e.Get(tag), Is.EqualTo("Fire"));
        }

        [Test]
        public void FloatTag_RoundTrips()
        {
            var tag = Bake(Make<FloatTag>(NextSlot(), "speed"));
            var e = new Entity();
            e.Set(tag, 1.5f);
            Assert.That(e.Get(tag), Is.EqualTo(1.5f));
        }

        [Test]
        public void PatternA_AndPatternC_CoexistOnSameEntity()
        {
            // The whole point of the prototype: code-defined Pattern A keys
            // and data-defined Pattern C tags share one slot space + one
            // entity surface, so handlers don't care which path defined the
            // tag.
            var codeKey = new TagKey<int>(NextSlot(), "code-tag");
            var dataTag = NewBakedIntTag("data-tag");

            var e = new Entity();
            e.Set(codeKey, 11);
            e.Set(dataTag, 22);

            Assert.That(e.Get(codeKey), Is.EqualTo(11));
            Assert.That(e.Get(dataTag), Is.EqualTo(22));
        }
    }
}
