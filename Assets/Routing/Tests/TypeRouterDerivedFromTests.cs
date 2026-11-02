using System;
using NUnit.Framework;

namespace Framework.Routing.Tests
{
    public class TypeRouterDerivedFromTests
    {
        readonly Probe _hello = new(typeof(Hello));
        readonly Probe _bye = new(typeof(Bye), order: -1);
        readonly Probe _genInt = new(typeof(Gen<int>));
        readonly Probe _genString = new(typeof(Gen<string>));

        TypeRouter<Probe> Router() => new(new[] { _hello, _bye, _genInt, _genString }, RouteMode.Multiple);

        [Test]
        public void BaseClass_ReturnsAllSubclassMembers_InMemberOrder()
        {
            Assert.That(Router().GetAllDerivedFrom(typeof(Note)), Is.EqualTo(new[] { _bye, _hello, _genInt, _genString }));
        }

        [Test]
        public void Interface_ReturnsImplementors()
        {
            Assert.That(Router().RoutedTypesDerivedFrom(typeof(ITagged)), Is.EqualTo(new[] { typeof(Bye) }));
        }

        [Test]
        public void OpenGenericDefinition_ReturnsClosedTypes()
        {
            Assert.That(Router().RoutedTypesDerivedFrom(typeof(Gen<>)), Is.EqualTo(new[] { typeof(Gen<int>), typeof(Gen<string>) }));
        }

        [Test]
        public void ExactType_IsIncluded()
        {
            Assert.That(Router().GetAllDerivedFrom(typeof(Hello)), Is.EqualTo(new[] { _hello }));
        }

        [Test]
        public void RoutedTypes_AreDistinct()
        {
            var router = new TypeRouter<Probe>(new[] { _hello, new Probe(typeof(Hello)) }, RouteMode.Multiple);
            Assert.That(router.RoutedTypesDerivedFrom(typeof(Note)), Is.EqualTo(new[] { typeof(Hello) }));
        }

        [Test]
        public void Miss_ReturnsEmpty()
        {
            Assert.That(Router().GetAllDerivedFrom(typeof(string)), Is.Empty);
            Assert.That(Router().RoutedTypesDerivedFrom(typeof(string)), Is.Empty);
        }
    }
}
