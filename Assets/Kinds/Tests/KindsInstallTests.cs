using System;
using NUnit.Framework;
using VContainer;

namespace Framework.Kinds.Tests
{
    public class KindsInstallTests
    {
        private static ContainerBuilder WithTests()
        {
            var builder = new ContainerBuilder();
            builder.AddKinds(o => o.IncludeTests = true);
            return builder;
        }

        [Test]
        public void AddKindSpace_Only_ResolvesTheFrozenRegistry()
        {
            var builder = WithTests();
            builder.AddKindSpace<Color, ColorRegistry>(c => c.Define(10, s => new CustomColor(s)));

            var colors = builder.Build().Resolve<ColorRegistry>();

            Assert.That(colors.SlotOf(colors.Of<Red>()), Is.EqualTo(1));
            Assert.That(colors.BySlot(10), Is.EqualTo(new CustomColor(10)));
            Assert.Throws<InvalidOperationException>(() => colors.Register(new CustomColor(11)));
        }

        [Test]
        public void AddKindSpace_ResolvesAsTheBaseRegistry_ToTheSameInstance()
        {
            var builder = WithTests();
            builder.AddKindSpace<Color, ColorRegistry>();

            var container = builder.Build();

            Assert.That(container.Resolve<TypeKey.Registry<Color>>(), Is.SameAs(container.Resolve<ColorRegistry>()));
        }

        [Test]
        public void AddKindSpace_Twice_GivesOneRegistryWithBothDataMembers()
        {
            var builder = WithTests();
            builder.AddKindSpace<Color, ColorRegistry>(c => c.Define(10, s => new CustomColor(s)));
            builder.AddKindSpace<Color, ColorRegistry>(c => c.Define(11, s => new CustomColor(s)));

            var container = builder.Build();
            var colors = container.Resolve<ColorRegistry>();

            Assert.That(container.Resolve<ColorRegistry>(), Is.SameAs(colors));
            Assert.That(colors.Members, Is.EqualTo(new Color[] { new Red(), new Green(), new CustomColor(10), new CustomColor(11) }));
        }

        [Test]
        public void AddKindSpace_InAChildScope_UsesTheParentRegistry()
        {
            var builder = WithTests();
            builder.AddKindSpace<Color, ColorRegistry>();
            var parent = builder.Build();

            using var child = parent.CreateScope(b => b.AddKindSpace<Color, ColorRegistry>());

            Assert.That(child.Resolve<ColorRegistry>(), Is.SameAs(parent.Resolve<ColorRegistry>()));
            Assert.Throws<InvalidOperationException>(() =>
                parent.CreateScope(b => b.AddKindSpace<Color, ColorRegistry>(c => c.Define(10, s => new CustomColor(s)))));
        }

        [Test]
        public void AddKindSpace_OtherRegistryForTheSameKey_ThrowsAndNamesBoth()
        {
            var builder = WithTests();
            builder.AddKindSpace<Color, ColorRegistry>();

            var e = Assert.Throws<InvalidOperationException>(() => builder.AddKindSpace<Color, TypeKey.Registry<Color>>());

            Assert.That(e.Message, Does.Contain(nameof(ColorRegistry)).And.Contain("Registry`1"));
        }

        [Test]
        public void AddKindSpace_OtherRegistryThanTheParentScope_ThrowsAndNamesBoth()
        {
            var builder = WithTests();
            builder.AddKindSpace<Color, ColorRegistry>();
            var parent = builder.Build();

            var e = Assert.Throws<InvalidOperationException>(() =>
                parent.CreateScope(b => b.AddKindSpace<Color, TypeKey.Registry<Color>>()));

            Assert.That(e.Message, Does.Contain(nameof(ColorRegistry)).And.Contain("Registry`1"));
        }

        [Test]
        public void AddKindSpace_WithoutAddKinds_UsesTheDefaults()
        {
            var defaults = new KindsOptions();
            Assert.That(defaults.IncludeTests, Is.False);
            Assert.That(defaults.Validate, Is.True);

            var builder = new ContainerBuilder();
            builder.AddKindSpace<Color, ColorRegistry>();

            Assert.That(builder.Build().Resolve<ColorRegistry>().Members, Is.Empty);
        }

        [Test]
        public void AddKinds_Only_ResolvesOptions()
        {
            var builder = new ContainerBuilder();
            builder.AddKinds(o => o.Validate = false);
            builder.AddKinds(o => o.IncludeTests = true);

            var options = builder.Build().Resolve<KindsOptions>();

            Assert.That(options.Validate, Is.False);
            Assert.That(options.IncludeTests, Is.True);
        }

        [Test]
        public void AddKindSpace_SlotClash_ThrowsAtContainerBuild()
        {
            var builder = WithTests();
            builder.AddKindSpace<ClashKind, TypeKey.Registry<ClashKind>>();

            var e = Assert.Catch(() => builder.Build());

            Assert.That(e.ToString(), Does.Contain(nameof(ClashA)).And.Contain(nameof(ClashB)));
        }

        [Test]
        public void AddKindSpace_ValidateOn_ThrowsOnAShapeIssue()
        {
            var builder = WithTests();
            builder.AddKindSpace<OpenKind, TypeKey.Registry<OpenKind>>();

            var e = Assert.Catch(() => builder.Build());

            Assert.That(e.ToString(), Does.Contain("KIND-02"));
        }

        [Test]
        public void AddKinds_ValidateOff_SkipsTheShapeChecks()
        {
            var builder = WithTests();
            builder.AddKinds(o => o.Validate = false);
            builder.AddKindSpace<OpenKind, TypeKey.Registry<OpenKind>>();

            Assert.That(builder.Build().Resolve<TypeKey.Registry<OpenKind>>().Members, Is.Empty);
        }
    }
}
