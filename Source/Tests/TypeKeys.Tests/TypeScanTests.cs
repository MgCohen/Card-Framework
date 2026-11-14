using TypeKeys.Domain;

namespace TypeKeys.Tests;

public sealed class TypeScanTests
{
    private static readonly Type[] FixtureReaders = { typeof(ByeReader), typeof(GenReader), typeof(HelloReader) };

    [Test]
    public void ConcreteTypesIn_FindsReaders_SkipsAbstractAndOpenGeneric()
    {
        Assert.That(TypeScan.ConcreteTypesIn(typeof(HelloReader).Assembly, typeof(INoteReader)), Is.EquivalentTo(FixtureReaders));
    }

    [Test]
    public void ConcreteTypesOf_Default_SkipsTestAssemblies()
    {
        Assert.That(TypeScan.ConcreteTypesOf(typeof(INoteReader)), Is.Empty);
    }

    [Test]
    public void ConcreteTypesOf_IncludeTests_FindsFixtureReaders_InNameOrder()
    {
        Assert.That(TypeScan.ConcreteTypesOf(typeof(INoteReader), includeTests: true), Is.EqualTo(FixtureReaders));
    }

    [Test]
    public void NamesThatReach_KeepsTwoLevelChain_SkipsUnrelated()
    {
        HashSet<string> reaching = TypeScan.NamesThatReach("TypeKeys.Domain", new (string, IEnumerable<string>)[]
        {
            ("Game", new[] { "Cards.Domain" }),
            ("Cards.Domain", new[] { "TypeKeys.Domain" }),
            ("TypeKeys.Domain", new[] { "System.Runtime" }),
            ("Other", new[] { "System.Runtime" }),
        });

        Assert.That(reaching, Is.EquivalentTo(new[] { "TypeKeys.Domain", "Cards.Domain", "Game" }));
    }

    [Test]
    public void Cache_KeysOnBaseTypeAndIncludeTests()
    {
        IReadOnlyList<Type> withTests = TypeScan.ConcreteTypesOf(typeof(INoteReader), true);

        Assert.That(TypeScan.ConcreteTypesOf(typeof(INoteReader), true), Is.SameAs(withTests));
        Assert.That(TypeScan.ConcreteTypesOf(typeof(INoteReader), false), Is.Not.SameAs(withTests));
    }
}
