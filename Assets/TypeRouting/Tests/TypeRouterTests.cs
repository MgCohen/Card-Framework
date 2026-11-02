using System;
using NUnit.Framework;

namespace Framework.TypeRouting.Tests
{
    public class TypeRouterTests
    {
        static TypeRouter<Probe> Router(RouteMode mode, params Probe[] members) => new(members, mode);

        [Test]
        public void Single_Duplicate_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                Router(RouteMode.Single, new Probe(typeof(Hello)), new Probe(typeof(Hello))));
            Assert.That(ex.Message, Is.EqualTo("Duplicate Probe for Hello."));
        }

        [Test]
        public void Multiple_Duplicate_KeepsBoth()
        {
            var router = Router(RouteMode.Multiple, new Probe(typeof(Hello)), new Probe(typeof(Hello)));
            Assert.That(router.GetAll(typeof(Hello)).Count, Is.EqualTo(2));
        }

        [Test]
        public void Get_Hit_ReturnsMember()
        {
            var hello = new Probe(typeof(Hello));
            Assert.That(Router(RouteMode.Single, hello).Get(typeof(Hello)), Is.SameAs(hello));
        }

        [Test]
        public void Get_Miss_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Router(RouteMode.Single).Get(typeof(Hello)));
            Assert.That(ex.Message, Is.EqualTo("No Probe registered for Hello."));
        }

        [Test]
        public void Get_MissOnGeneric_NamesTypeArguments()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => Router(RouteMode.Single).Get(typeof(Gen<int>)));
            Assert.That(ex.Message, Is.EqualTo("No Probe registered for Gen<Int32>."));
        }

        [Test]
        public void Get_Many_Throws()
        {
            var router = Router(RouteMode.Multiple, new Probe(typeof(Hello)), new Probe(typeof(Hello)));
            Assert.Throws<InvalidOperationException>(() => router.Get(typeof(Hello)));
        }

        [Test]
        public void TryGet_Miss_ReturnsFalse()
        {
            Assert.That(Router(RouteMode.Single).TryGet(typeof(Hello), out var member), Is.False);
            Assert.That(member, Is.Null);
        }

        [Test]
        public void TryGet_Many_Throws()
        {
            var router = Router(RouteMode.Multiple, new Probe(typeof(Hello)), new Probe(typeof(Hello)));
            Assert.Throws<InvalidOperationException>(() => router.TryGet(typeof(Hello), out _));
        }

        [Test]
        public void GetAll_Miss_ReturnsEmpty()
        {
            Assert.That(Router(RouteMode.Multiple).GetAll(typeof(Hello)), Is.Empty);
        }

        [Test]
        public void GetAll_SortsByOrderThenFullTypeName()
        {
            var plainLate = new Probe(typeof(Hello));
            var otherLate = new OtherProbe(typeof(Hello));
            var early = new Probe(typeof(Hello), order: -1);

            var router = Router(RouteMode.Multiple, plainLate, otherLate, early);

            // "...OtherProbe" sorts before "...Probe" (ordinal).
            Assert.That(router.GetAll(typeof(Hello)), Is.EqualTo(new Probe[] { early, otherLate, plainLate }));
        }

        [Test]
        public void RoutedTypes_ListsEachRouteTypeOnce()
        {
            var router = Router(RouteMode.Multiple, new Probe(typeof(Hello)), new Probe(typeof(Hello)), new Probe(typeof(Bye)));
            Assert.That(router.RoutedTypes, Is.EquivalentTo(new[] { typeof(Hello), typeof(Bye) }));
        }

        [Test]
        public void Members_MustBeIRouted()
        {
            var constraints = typeof(TypeRouter<>).GetGenericArguments()[0].GetGenericParameterConstraints();
            Assert.That(constraints, Does.Contain(typeof(IRouted)));
        }
    }
}
