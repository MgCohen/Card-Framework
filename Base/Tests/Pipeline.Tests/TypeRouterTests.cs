using Pipeline.Slices.Run;

namespace Pipeline.Tests;

public sealed class TypeRouterTests
{
    [Test]
    public void Single_Duplicate_Throws()
    {
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(new Action(() =>
            Router(RouteMode.Single, new Probe(typeof(Hello)), new Probe(typeof(Hello)))))!;

        Assert.That(e.Message, Is.EqualTo("Duplicate Probe for Hello."));
    }

    [Test]
    public void Multiple_Duplicate_KeepsBoth()
    {
        TypeRouter<Probe> router = Router(RouteMode.Multiple, new Probe(typeof(Hello)), new Probe(typeof(Hello)));

        Assert.That(router.GetAll(typeof(Hello)), Has.Count.EqualTo(2));
    }

    [Test]
    public void Get_Hit_ReturnsMember()
    {
        Probe hello = new Probe(typeof(Hello));

        Assert.That(Router(RouteMode.Single, hello).Get(typeof(Hello)), Is.SameAs(hello));
    }

    [Test]
    public void Get_Miss_Throws()
    {
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(
            new Action(() => Router(RouteMode.Single).Get(typeof(Hello))))!;

        Assert.That(e.Message, Is.EqualTo("No Probe registered for Hello."));
    }

    [Test]
    public void Get_MissOnGeneric_NamesTypeArguments()
    {
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(
            new Action(() => Router(RouteMode.Single).Get(typeof(Gen<int>))))!;

        Assert.That(e.Message, Is.EqualTo("No Probe registered for Gen<Int32>."));
    }

    [Test]
    public void Get_Many_Throws()
    {
        TypeRouter<Probe> router = Router(RouteMode.Multiple, new Probe(typeof(Hello)), new Probe(typeof(Hello)));

        Assert.Throws<InvalidOperationException>(new Action(() => router.Get(typeof(Hello))));
    }

    [Test]
    public void TryGet_Miss_ReturnsFalse()
    {
        Assert.That(Router(RouteMode.Single).TryGet(typeof(Hello), out Probe? member), Is.False);
        Assert.That(member, Is.Null);
    }

    [Test]
    public void TryGet_Many_Throws()
    {
        TypeRouter<Probe> router = Router(RouteMode.Multiple, new Probe(typeof(Hello)), new Probe(typeof(Hello)));

        Assert.Throws<InvalidOperationException>(new Action(() => router.TryGet(typeof(Hello), out _)));
    }

    [Test]
    public void GetAll_Miss_ReturnsEmpty()
    {
        Assert.That(Router(RouteMode.Multiple).GetAll(typeof(Hello)), Is.Empty);
    }

    [Test]
    public void GetAll_SortsByOrderThenFullTypeName()
    {
        Probe plainLate = new Probe(typeof(Hello));
        Probe otherLate = new OtherProbe(typeof(Hello));
        Probe early = new Probe(typeof(Hello), order: -1);

        TypeRouter<Probe> router = Router(RouteMode.Multiple, plainLate, otherLate, early);

        Assert.That(router.GetAll(typeof(Hello)), Is.EqualTo(new[] { early, otherLate, plainLate }));
    }

    [Test]
    public void RoutedTypes_ListsEachRouteTypeOnce()
    {
        TypeRouter<Probe> router = Router(
            RouteMode.Multiple, new Probe(typeof(Hello)), new Probe(typeof(Hello)), new Probe(typeof(Bye)));

        Assert.That(router.RoutedTypes, Is.EquivalentTo(new[] { typeof(Hello), typeof(Bye) }));
    }

    [Test]
    public void DerivedFrom_BaseClass_ReturnsAllSubclassMembers_InMemberOrder()
    {
        Probe hello = new Probe(typeof(Hello));
        Probe bye = new Probe(typeof(Bye), order: -1);
        Probe genInt = new Probe(typeof(Gen<int>));
        Probe genString = new Probe(typeof(Gen<string>));

        TypeRouter<Probe> router = Router(RouteMode.Multiple, hello, bye, genInt, genString);

        Assert.That(router.GetAllDerivedFrom(typeof(Note)), Is.EqualTo(new[] { bye, hello, genInt, genString }));
        Assert.That(router.RoutedTypesDerivedFrom(typeof(ITagged)), Is.EqualTo(new[] { typeof(Bye) }));
        Assert.That(
            router.RoutedTypesDerivedFrom(typeof(Gen<>)),
            Is.EqualTo(new[] { typeof(Gen<int>), typeof(Gen<string>) }));
        Assert.That(router.GetAllDerivedFrom(typeof(string)), Is.Empty);
    }

    private static TypeRouter<Probe> Router(RouteMode mode, params Probe[] members)
    {
        return new TypeRouter<Probe>(members, mode);
    }
}
