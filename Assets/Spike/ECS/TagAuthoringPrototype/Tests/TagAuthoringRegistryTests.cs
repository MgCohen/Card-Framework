#nullable enable
using System;
using System.Linq;
using NUnit.Framework;
using TagAuthoringPrototype.Authoring;
using VerticalSlice.Engine.Tags;
using static TagAuthoringPrototype.Tests.TagTestSupport;

namespace TagAuthoringPrototype.Tests
{
    /// <summary>
    /// <see cref="TagAuthoringRegistry"/> behavior. Note: <c>Count</c> and
    /// <c>All</c> reflect process-global state — tests don't assert on them
    /// directly because other tests in the same Unity domain contribute.
    /// </summary>
    public class TagAuthoringRegistryTests
    {
        [Test]
        public void Bake_StoresDefForLookupBySlot()
        {
            var slot = NextSlot();
            var tag = Make<IntTag>(slot, $"id-{slot}");
            TagAuthoringRegistry.Bake(new[] { tag });

            Assert.That(TagAuthoringRegistry.GetDefinition(slot), Is.SameAs(tag));
        }

        [Test]
        public void Bake_StoresDefForLookupById()
        {
            var slot = NextSlot();
            var id = $"id-{slot}";
            var tag = Make<IntTag>(slot, id);
            TagAuthoringRegistry.Bake(new[] { tag });

            Assert.That(TagAuthoringRegistry.GetDefinition(id), Is.SameAs(tag));
        }

        [Test]
        public void Bake_StoresDefForLookupByKey()
        {
            var slot = NextSlot();
            var tag = Make<IntTag>(slot, $"id-{slot}");
            TagAuthoringRegistry.Bake(new[] { tag });

            Assert.That(TagAuthoringRegistry.GetDefinition(tag.Key), Is.SameAs(tag));
        }

        [Test]
        public void GetDefinition_Unknown_ReturnsNull()
        {
            Assert.That(TagAuthoringRegistry.GetDefinition(slot: -123),    Is.Null);
            Assert.That(TagAuthoringRegistry.GetDefinition(id: "nope-xyz"), Is.Null);
        }

        [Test]
        public void GetDefinition_ByKey_CodeDefinedKey_ReturnsNull()
        {
            // Pattern A — code-defined key, never seen by TagAuthoringRegistry.
            var codeKey = new TagKey<int>(NextSlot(), "code-only");

            Assert.That(TagAuthoringRegistry.GetDefinition(codeKey), Is.Null);
        }

        [Test]
        public void Bake_NullEnumerable_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => TagAuthoringRegistry.Bake(null!));
        }

        [Test]
        public void Bake_NullEntries_AreSkipped()
        {
            var slot = NextSlot();
            var tag = Make<IntTag>(slot, $"id-{slot}");
            Assert.DoesNotThrow(() => TagAuthoringRegistry.Bake(new TagDefinition?[] { null, tag, null }));
            Assert.That(TagAuthoringRegistry.GetDefinition(slot), Is.SameAs(tag));
        }

        [Test]
        public void Bake_EmptyId_Throws()
        {
            var tag = Make<IntTag>(NextSlot(), "");
            Assert.Throws<InvalidOperationException>(
                () => TagAuthoringRegistry.Bake(new[] { tag }));
        }

        [Test]
        public void Bake_ZeroSlot_Throws()
        {
            var tag = Make<IntTag>(0, "zero-slot");
            Assert.Throws<InvalidOperationException>(
                () => TagAuthoringRegistry.Bake(new[] { tag }));
        }

        [Test]
        public void Bake_NegativeSlot_Throws()
        {
            var tag = Make<IntTag>(-5, "neg-slot");
            Assert.Throws<InvalidOperationException>(
                () => TagAuthoringRegistry.Bake(new[] { tag }));
        }

        [Test]
        public void Bake_SameInstanceTwice_IsIdempotent()
        {
            var slot = NextSlot();
            var tag = Make<IntTag>(slot, $"id-{slot}");
            TagAuthoringRegistry.Bake(new[] { tag });

            Assert.DoesNotThrow(() => TagAuthoringRegistry.Bake(new[] { tag }));
            Assert.That(TagAuthoringRegistry.GetDefinition(slot), Is.SameAs(tag));
        }

        [Test]
        public void Bake_TwoDefsSameSlot_SecondBakeThrows()
        {
            var slot = NextSlot();
            var a = Make<IntTag>(slot, "a");
            var b = Make<IntTag>(slot, "b");

            TagAuthoringRegistry.Bake(new[] { a });
            Assert.Throws<InvalidOperationException>(
                () => TagAuthoringRegistry.Bake(new[] { b }));
        }

        [Test]
        public void Bake_MultipleDefs_AllMaterialized()
        {
            var hSlot = NextSlot();
            var mSlot = NextSlot();
            var oSlot = NextSlot();
            var health  = Make<IntTag>   (hSlot, $"h-{hSlot}");
            var mana    = Make<IntTag>   (mSlot, $"m-{mSlot}");
            var onFire  = Make<BoolTag>  (oSlot, $"o-{oSlot}");

            TagAuthoringRegistry.Bake(new TagDefinition[] { health, mana, onFire });

            Assert.That(TagAuthoringRegistry.GetDefinition(hSlot), Is.SameAs(health));
            Assert.That(TagAuthoringRegistry.GetDefinition(mSlot), Is.SameAs(mana));
            Assert.That(TagAuthoringRegistry.GetDefinition(oSlot), Is.SameAs(onFire));
            // Materialize was called for each — Key is non-null.
            Assert.DoesNotThrow(() => { var _ = health.Key; });
            Assert.DoesNotThrow(() => { var _ = mana.Key; });
            Assert.DoesNotThrow(() => { var _ = onFire.Key; });
        }

        [Test]
        public void All_IncludesBakedDef()
        {
            var slot = NextSlot();
            var tag = Make<IntTag>(slot, $"id-{slot}");
            TagAuthoringRegistry.Bake(new[] { tag });

            Assert.That(TagAuthoringRegistry.All.Any(d => ReferenceEquals(d, tag)), Is.True);
        }
    }
}
