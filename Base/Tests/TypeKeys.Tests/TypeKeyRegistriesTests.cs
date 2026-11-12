using TypeKeys.Domain;

namespace TypeKeys.Tests;

public sealed class TypeKeyRegistriesTests
{
    [Test]
    public void Build_RootWithNoCustomRegistry_RegistersTheBaseRegistry()
    {
        IReadOnlyList<TypeKeyRegistration> registrations = TypeKeyRegistries.Build(true, typeof(Fruit));

        Assert.That(registrations, Has.Count.EqualTo(1));
        Assert.That(registrations[0].Service, Is.EqualTo(typeof(TypeKeyRegistry<Fruit>)));
        Assert.That(((TypeKeyRegistry<Fruit>)registrations[0].Registry).Members, Is.EqualTo(new Fruit[] { new Apple() }));
    }

    [Test]
    public void Build_RootWithACustomRegistry_RegistersBothServicesToOneInstance()
    {
        IReadOnlyList<TypeKeyRegistration> registrations = TypeKeyRegistries.Build(true, typeof(Color));

        Assert.That(registrations.Select(r => r.Service), Is.EquivalentTo(new[] { typeof(ColorRegistry), typeof(TypeKeyRegistry<Color>) }));
        Assert.That(registrations[0].Registry, Is.SameAs(registrations[1].Registry));
        Assert.That(registrations[0].Registry, Is.InstanceOf<ColorRegistry>());
    }

    [Test]
    public void Build_FillsAndFreezesTheRegistry()
    {
        ColorRegistry colors = (ColorRegistry)TypeKeyRegistries.Build(true, typeof(Color))[0].Registry;

        Assert.That(colors.Members, Is.EqualTo(new Color[] { new Red(), new Green() }));
        Assert.Throws<InvalidOperationException>(new Action(() => colors.Define(10, s => new CustomColor(s))));
    }

    [Test]
    public void Build_TwoCustomRegistries_ThrowsAndNamesBoth()
    {
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(new Action(() => TypeKeyRegistries.Build(true, typeof(Shape))))!;

        Assert.That(e.Message, Does.Contain(nameof(ShapeRegistryA)).And.Contain(nameof(ShapeRegistryB)));
    }

    [Test]
    public void Build_SlotClash_ThrowsAndNamesBothMembers()
    {
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(new Action(() => TypeKeyRegistries.Build(true, typeof(ClashKey))))!;

        Assert.That(e.Message, Does.Contain("TYPEKEY-01").And.Contain(nameof(ClashA)).And.Contain(nameof(ClashB)));
    }

    [Test]
    public void Build_BadShape_Throws()
    {
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(new Action(() => TypeKeyRegistries.Build(true, typeof(OpenKey))))!;

        Assert.That(e.Message, Does.Contain("TYPEKEY-02"));
    }

    [Test]
    public void Build_NoRoots_ScansEachRoot_AndThrowsOnTheBadFixtures()
    {
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(new Action(() => TypeKeyRegistries.Build(true)))!;

        Assert.That(e.Message, Does.Contain(nameof(ClashKey)));
    }

    [Test]
    public void Build_WithoutTests_SkipsTheTestAssembly()
    {
        Assert.That(TypeKeyRegistries.Build(false), Is.Empty);
    }
}
