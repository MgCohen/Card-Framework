#nullable enable
using NUnit.Framework;
using UnityEngine;
using TagAuthoringPrototype.Authoring;
using static TagAuthoringPrototype.Tests.TagTestSupport;

namespace TagAuthoringPrototype.Tests
{
    public class TagPresentationTests
    {
        [Test]
        public void Default_IsEmpty()
        {
            var p = new TagPresentation();
            Assert.That(p.IsEmpty, Is.True);
            Assert.That(p.Label,   Is.EqualTo(""));
            Assert.That(p.Tooltip, Is.EqualTo(""));
            Assert.That(p.Icon,    Is.Null);
        }

        [Test]
        public void SettingLabel_MakesNonEmpty()
        {
            var p = MakePresentation(label: "Health");
            Assert.That(p.IsEmpty, Is.False);
            Assert.That(p.Label,   Is.EqualTo("Health"));
        }

        [Test]
        public void SettingTooltip_MakesNonEmpty()
        {
            var p = MakePresentation(tooltip: "current hit points");
            Assert.That(p.IsEmpty, Is.False);
            Assert.That(p.Tooltip, Is.EqualTo("current hit points"));
        }

        [Test]
        public void Color_DoesNotAffectIsEmpty()
        {
            // IsEmpty only inspects icon/label/tooltip — a tag with just a
            // custom swatch and no label/icon is still considered "no
            // presentation authored." Documented in TagPresentation.cs.
            var p = MakePresentation(color: Color.red);
            Assert.That(p.IsEmpty, Is.True);
            Assert.That(p.Color,   Is.EqualTo(Color.red));
        }

        [Test]
        public void DefaultColor_IsWhite()
        {
            var p = new TagPresentation();
            Assert.That(p.Color, Is.EqualTo(Color.white));
        }

        [Test]
        public void AllFieldsSet_ExposedThroughGetters()
        {
            var p = MakePresentation(label: "Mana", tooltip: "current mana", color: Color.blue);
            Assert.That(p.Label,   Is.EqualTo("Mana"));
            Assert.That(p.Tooltip, Is.EqualTo("current mana"));
            Assert.That(p.Color,   Is.EqualTo(Color.blue));
            Assert.That(p.IsEmpty, Is.False);
        }
    }
}
