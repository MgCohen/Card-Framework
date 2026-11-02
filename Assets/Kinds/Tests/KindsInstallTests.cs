using System;
using NUnit.Framework;
using VContainer;

namespace Framework.Kinds.Tests
{
    public class KindsInstallTests
    {
        [Test]
        public void AddKindSpace_Only_ResolvesTheFrozenRegistry()
        {
            var builder = new ContainerBuilder();
            builder.AddKindSpace<Color, ColorRegistry>(c => c.Define(10, s => new CustomColor(s)));

            var colors = builder.Build().Resolve<ColorRegistry>();

            Assert.That(colors.SlotOf(colors.Of<Red>()), Is.EqualTo(1));
            Assert.That(colors.BySlot(10), Is.EqualTo(new CustomColor(10)));
            Assert.Throws<InvalidOperationException>(() => colors.Register(new CustomColor(11)));
        }

        [Test]
        public void AddKindSpace_Twice_GivesOneRegistryWithBothDataMembers()
        {
            var builder = new ContainerBuilder();
            builder.AddKindSpace<Color, ColorRegistry>(c => c.Define(10, s => new CustomColor(s)));
            builder.AddKindSpace<Color, ColorRegistry>(c => c.Define(11, s => new CustomColor(s)));

            var container = builder.Build();
            var colors = container.Resolve<ColorRegistry>();

            Assert.That(container.Resolve<ColorRegistry>(), Is.SameAs(colors));
            Assert.That(colors.Members, Is.EqualTo(new Color[] { new Red(), new Green(), new CustomColor(10), new CustomColor(11) }));
        }

        [Test]
        public void AddKindSpace_WithoutAddKinds_UsesTheDefaults()
        {
            var defaults = new KindsOptions();
            Assert.That(defaults.IncludeTests, Is.True);
            Assert.That(defaults.Validate, Is.True);

            var builder = new ContainerBuilder();
            builder.AddKindSpace<Color, ColorRegistry>();

            Assert.That(builder.Build().Resolve<ColorRegistry>().Members, Has.Count.EqualTo(2));
        }

        [Test]
        public void AddKinds_IncludeTestsOff_ScansNoTestMember()
        {
            var builder = new ContainerBuilder();
            builder.AddKinds(o => o.IncludeTests = false);
            builder.AddKindSpace<Color, ColorRegistry>();

            Assert.That(builder.Build().Resolve<ColorRegistry>().Members, Is.Empty);
        }

        [Test]
        public void AddKindSpace_SlotClash_ThrowsAtContainerBuild()
        {
            var builder = new ContainerBuilder();
            builder.AddKindSpace<ClashKind, TypeKey.Registry<ClashKind>>();

            var e = Assert.Catch(() => builder.Build());

            Assert.That(e.ToString(), Does.Contain(nameof(ClashA)).And.Contain(nameof(ClashB)));
        }

        [Test]
        public void AddKindSpace_ValidateOn_ThrowsOnAShapeIssue()
        {
            var builder = new ContainerBuilder();
            builder.AddKindSpace<OpenKind, TypeKey.Registry<OpenKind>>();

            var e = Assert.Catch(() => builder.Build());

            Assert.That(e.ToString(), Does.Contain("KIND-02"));
        }

        [Test]
        public void AddKinds_ValidateOff_SkipsTheShapeChecks()
        {
            var builder = new ContainerBuilder();
            builder.AddKinds(o => o.Validate = false);
            builder.AddKindSpace<OpenKind, TypeKey.Registry<OpenKind>>();

            Assert.That(builder.Build().Resolve<TypeKey.Registry<OpenKind>>().Members, Is.Empty);
        }
    }
}
