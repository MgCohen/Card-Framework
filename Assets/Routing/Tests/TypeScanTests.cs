using NUnit.Framework;

namespace Framework.Routing.Tests
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
        public void Cache_KeysOnBaseTypeAndIncludeTests()
        {
            var withTests = TypeScan.ConcreteTypesOf(typeof(INoteReader), true);

            Assert.That(TypeScan.ConcreteTypesOf(typeof(INoteReader), true), Is.SameAs(withTests));
            Assert.That(TypeScan.ConcreteTypesOf(typeof(INoteReader), false), Is.Not.SameAs(withTests));
        }
    }
}
