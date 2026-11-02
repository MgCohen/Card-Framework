using NUnit.Framework;

namespace Framework.TypeRouting.Tests
{
    public class TypeScanTests
    {
        static readonly System.Type[] FixtureReaders = { typeof(ByeReader), typeof(GenReader), typeof(HelloReader) };

        [Test]
        public void ConcreteTypesIn_FindsReaders_SkipsAbstractAndOpenGeneric()
        {
            Assert.That(TypeScan.ConcreteTypesIn(typeof(HelloReader).Assembly, typeof(INoteReader)),
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
            // Game derives a Pipeline class and references only Pipeline. It is listed first, so one pass is not enough.
            var reaching = TypeScan.NamesThatReach("Framework.TypeRouting", new (string, System.Collections.Generic.IEnumerable<string>)[]
            {
                ("Game", new[] { "Framework.Pipeline" }),
                ("Framework.Pipeline", new[] { "Framework.TypeRouting" }),
                ("Framework.TypeRouting", new[] { "VContainer" }),
                ("Other", new[] { "VContainer" }),
            });

            Assert.That(reaching, Is.EquivalentTo(new[] { "Framework.TypeRouting", "Framework.Pipeline", "Game" }));
        }

        [Test]
        public void Cache_KeysOnBaseTypeAndIncludeTests()
        {
            var withTests = TypeScan.ConcreteTypesOf(typeof(INoteReader), true);

            Assert.That(TypeScan.ConcreteTypesOf(typeof(INoteReader), true), Is.SameAs(withTests));
            Assert.That(TypeScan.ConcreteTypesOf(typeof(INoteReader), false), Is.Not.SameAs(withTests));
        }
    }
}
