using Pipeline.Slices.Run;

namespace Pipeline.Tests;

public sealed class TypeScanTests
{
    private static readonly Type[] FixtureReaders = { typeof(ByeReader), typeof(GenReader), typeof(HelloReader) };

    [Test]
    public void ConcreteTypesIn_FindsReaders_SkipsAbstractAndOpenGeneric()
    {
        Assert.That(
            TypeScan.ConcreteTypesIn(typeof(HelloReader).Assembly, typeof(INoteReader)),
            Is.EquivalentTo(FixtureReaders));
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
        HashSet<string> reaching = TypeScan.NamesThatReach("Pipeline.Handlers.Commands", new (string, IEnumerable<string>)[]
        {
            ("Game", new[] { "Cards.Handlers.Commands" }),
            ("Cards.Handlers.Commands", new[] { "Pipeline.Handlers.Commands" }),
            ("Pipeline.Handlers.Commands", new[] { "System.Runtime" }),
            ("Other", new[] { "System.Runtime" }),
        });

        Assert.That(reaching, Is.EquivalentTo(new[] { "Pipeline.Handlers.Commands", "Cards.Handlers.Commands", "Game" }));
    }

    [Test]
    public void Cache_KeysOnBaseTypeAndIncludeTests()
    {
        IReadOnlyList<Type> withTests = TypeScan.ConcreteTypesOf(typeof(INoteReader), true);

        Assert.That(TypeScan.ConcreteTypesOf(typeof(INoteReader), true), Is.SameAs(withTests));
        Assert.That(TypeScan.ConcreteTypesOf(typeof(INoteReader), false), Is.Not.SameAs(withTests));
    }
}
