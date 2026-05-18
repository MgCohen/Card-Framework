#nullable enable
using System;
using NUnit.Framework;
using TagAuthoringPrototype.Authoring;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Tags;
using static TagAuthoringPrototype.Tests.TagTestSupport;

namespace TagAuthoringPrototype.Tests
{
    public class TagDefinitionTests
    {
        [Test]
        public void Key_BeforeMaterialize_Throws()
        {
            var tag = Make<IntTag>(NextSlot(), "unbaked");
            Assert.Throws<InvalidOperationException>(() => { var _ = tag.Key; });
        }

        [Test]
        public void Materialize_ReturnsTypedKey_AtAuthoredSlot()
        {
            var slot = NextSlot();
            var tag = Make<IntTag>(slot, "health");

            var key = tag.Materialize();

            Assert.That(key, Is.InstanceOf<TagKey<int>>());
            Assert.That(key.Slot, Is.EqualTo(slot));
            Assert.That(key.Name, Is.EqualTo("health"));
            Assert.That(key.ValueType, Is.EqualTo(typeof(int)));
        }

        [Test]
        public void Materialize_IsIdempotent_ReturnsSameKeyInstance()
        {
            var tag = Make<IntTag>(NextSlot(), "id");
            var k1 = tag.Materialize();
            var k2 = tag.Materialize();
            Assert.That(k2, Is.SameAs(k1));
            Assert.That(tag.Key, Is.SameAs(k1));
        }

        [Test]
        public void Materialize_ClaimsSlotInGlobalTagRegistry()
        {
            var slot = NextSlot();
            var tag = Make<IntTag>(slot, "claimed");
            var key = tag.Materialize();

            Assert.That(TagRegistry.GetBySlot(slot), Is.SameAs(key));
        }

        [Test]
        public void Materialize_TwoDefsSameSlot_SecondThrows()
        {
            var slot = NextSlot();
            var a = Make<IntTag>(slot, "a");
            var b = Make<IntTag>(slot, "b");

            a.Materialize();
            Assert.Throws<InvalidOperationException>(() => b.Materialize());
        }

        [Test]
        public void TypedSubclass_ExposesValueTypeFromGenericArg()
        {
            Assert.That(Make<IntTag>(NextSlot(),   "i").ValueType, Is.EqualTo(typeof(int)));
            Assert.That(Make<BoolTag>(NextSlot(),  "b").ValueType, Is.EqualTo(typeof(bool)));
            Assert.That(Make<FloatTag>(NextSlot(), "f").ValueType, Is.EqualTo(typeof(float)));
            Assert.That(Make<StringTag>(NextSlot(),"s").ValueType, Is.EqualTo(typeof(string)));
        }

        [Test]
        public void ImplicitConversion_ToTagKey_YieldsMaterializedKey()
        {
            var tag = Make<IntTag>(NextSlot(), "imp");
            tag.Materialize();

            // Implicit op TagDefinition<int> → TagKey<int>.
            TagKey<int> key = tag;

            Assert.That(key, Is.SameAs(tag.Key));
        }

        [Test]
        public void ImplicitConversion_BeforeMaterialize_Throws()
        {
            var tag = Make<IntTag>(NextSlot(), "unbaked");
            Assert.Throws<InvalidOperationException>(() =>
            {
                TagKey<int> _ = tag;
            });
        }

        [Test]
        public void ImplicitKey_DrivesEntitySetGet_LikeCodeDefinedTag()
        {
            var tag = Make<IntTag>(NextSlot(), "health");
            tag.Materialize();

            // Cast routes through the implicit op explicitly — separate from
            // the extension-method overload tested elsewhere.
            TagKey<int> key = tag;
            var e = new Entity();
            e.Set(key, 30);

            Assert.That(e.Get(key), Is.EqualTo(30));
        }

        [Test]
        public void Properties_MirrorAuthoredFields()
        {
            var pres = MakePresentation(label: "Health", tooltip: "hp", color: UnityEngine.Color.red);
            var tag = Make<IntTag>(slot: NextSlot(), id: "health", presentation: pres);

            Assert.That(tag.Id, Is.EqualTo("health"));
            Assert.That(tag.Slot, Is.GreaterThanOrEqualTo(70000));
            Assert.That(tag.Presentation, Is.SameAs(pres));
        }
    }
}
