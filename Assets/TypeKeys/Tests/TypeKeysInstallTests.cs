using System;
using NUnit.Framework;
using VContainer;

namespace Framework.TypeKeys.Tests
{
    public class TypeKeysInstallTests
    {
        private static ContainerBuilder WithTests()
        {
            var builder = new ContainerBuilder();
            builder.AddTypeKeys(o => o.IncludeTests = true);
            return builder;
        }

        [Test]
        public void AddKeySpace_Only_ResolvesTheFrozenRegistry()
        {
            var builder = WithTests();
            builder.AddKeySpace<Color, ColorRegistry>(c => c.Define(10, s => new CustomColor(s)));

            var colors = builder.Build().Resolve<ColorRegistry>();

            Assert.That(colors.SlotOf(colors.Of<Red>()), Is.EqualTo(1));
            Assert.That(colors.BySlot(10), Is.EqualTo(new CustomColor(10)));
            Assert.Throws<InvalidOperationException>(() => colors.Register(new CustomColor(11)));
        }

        [Test]
        public void AddKeySpace_ResolvesAsTheBaseRegistry_ToTheSameInstance()
        {
            var builder = WithTests();
            builder.AddKeySpace<Color, ColorRegistry>();

            var container = builder.Build();

            Assert.That(container.Resolve<TypeKey.Registry<Color>>(), Is.SameAs(container.Resolve<ColorRegistry>()));
        }

        [Test]
        public void AddKeySpace_Twice_GivesOneRegistryWithBothDataMembers()
        {
            var builder = WithTests();
            builder.AddKeySpace<Color, ColorRegistry>(c => c.Define(10, s => new CustomColor(s)));
            builder.AddKeySpace<Color, ColorRegistry>(c => c.Define(11, s => new CustomColor(s)));

            var container = builder.Build();
            var colors = container.Resolve<ColorRegistry>();

            Assert.That(container.Resolve<ColorRegistry>(), Is.SameAs(colors));
            Assert.That(colors.Members, Is.EqualTo(new Color[] { new Red(), new Green(), new CustomColor(10), new CustomColor(11) }));
        }

        [Test]
        public void AddKeySpace_InAChildScope_UsesTheParentRegistry()
        {
            var builder = WithTests();
            builder.AddKeySpace<Color, ColorRegistry>();
            var parent = builder.Build();

            using var child = parent.CreateScope(b => b.AddKeySpace<Color, ColorRegistry>());

            Assert.That(child.Resolve<ColorRegistry>(), Is.SameAs(parent.Resolve<ColorRegistry>()));
            Assert.Throws<InvalidOperationException>(() =>
                parent.CreateScope(b => b.AddKeySpace<Color, ColorRegistry>(c => c.Define(10, s => new CustomColor(s)))));
        }

        [Test]
        public void AddKeySpace_OtherRegistryForTheSameKey_ThrowsAndNamesBoth()
        {
            var builder = WithTests();
            builder.AddKeySpace<Color, ColorRegistry>();

            var e = Assert.Throws<InvalidOperationException>(() => builder.AddKeySpace<Color, TypeKey.Registry<Color>>());

            Assert.That(e.Message, Does.Contain(nameof(ColorRegistry)).And.Contain("Registry`1"));
        }

        [Test]
        public void AddKeySpace_OtherRegistryThanTheParentScope_ThrowsAndNamesBoth()
        {
            var builder = WithTests();
            builder.AddKeySpace<Color, ColorRegistry>();
            var parent = builder.Build();

            var e = Assert.Throws<InvalidOperationException>(() =>
                parent.CreateScope(b => b.AddKeySpace<Color, TypeKey.Registry<Color>>()));

            Assert.That(e.Message, Does.Contain(nameof(ColorRegistry)).And.Contain("Registry`1"));
        }

        [Test]
        public void AddKeySpace_WithoutAddTypeKeys_UsesTheDefaults()
        {
            var defaults = new TypeKeysOptions();
            Assert.That(defaults.IncludeTests, Is.False);
            Assert.That(defaults.Validate, Is.True);

            var builder = new ContainerBuilder();
            builder.AddKeySpace<Color, ColorRegistry>();

            Assert.That(builder.Build().Resolve<ColorRegistry>().Members, Is.Empty);
        }

        [Test]
        public void AddTypeKeys_Only_ResolvesOptions()
        {
            var builder = new ContainerBuilder();
            builder.AddTypeKeys(o => o.Validate = false);
            builder.AddTypeKeys(o => o.IncludeTests = true);

            var options = builder.Build().Resolve<TypeKeysOptions>();

            Assert.That(options.Validate, Is.False);
            Assert.That(options.IncludeTests, Is.True);
        }

        [Test]
        public void AddTypeKeys_NoConfigure_ResolvesDefaultOptions()
        {
            var builder = new ContainerBuilder();
            builder.AddTypeKeys();

            Assert.That(builder.Build().Resolve<TypeKeysOptions>().IncludeTests, Is.False);
        }

        [Test]
        public void AddKeySpace_SlotClash_ThrowsAtContainerBuild()
        {
            var builder = WithTests();
            builder.AddKeySpace<ClashKey, TypeKey.Registry<ClashKey>>();

            var e = Assert.Catch(() => builder.Build());

            Assert.That(e.ToString(), Does.Contain(nameof(ClashA)).And.Contain(nameof(ClashB)));
        }

        [Test]
        public void AddKeySpace_ValidateOn_ThrowsOnAShapeIssue()
        {
            var builder = WithTests();
            builder.AddKeySpace<OpenKey, TypeKey.Registry<OpenKey>>();

            var e = Assert.Catch(() => builder.Build());

            Assert.That(e.ToString(), Does.Contain("TYPEKEY-02"));
        }

        [Test]
        public void AddTypeKeys_ValidateOff_SkipsTheShapeChecks()
        {
            var builder = WithTests();
            builder.AddTypeKeys(o => o.Validate = false);
            builder.AddKeySpace<OpenKey, TypeKey.Registry<OpenKey>>();

            Assert.That(builder.Build().Resolve<TypeKey.Registry<OpenKey>>().Members, Is.Empty);
        }
    }
}
