using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Framework.Routing.Tests
{
    public class TypeRouterAllocationTests
    {
        readonly TypeRouter<Probe> _router =
            new(new[] { new Probe(typeof(Hello)), new Probe(typeof(Bye)) }, RouteMode.Multiple);

        [Test]
        public void GetAll_DoesNotAllocate()
        {
            _router.GetAll(typeof(Hello));
            _router.GetAll(typeof(Gen<int>));
            Assert.That(() =>
            {
                _router.GetAll(typeof(Hello));
                _router.GetAll(typeof(Gen<int>));
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void DerivedReads_DoNotAllocate()
        {
            _router.GetAllDerivedFrom(typeof(Note));
            _router.RoutedTypesDerivedFrom(typeof(Note));
            Assert.That(() =>
            {
                _router.GetAllDerivedFrom(typeof(Note));
                _router.RoutedTypesDerivedFrom(typeof(Note));
                _router.GetAllDerivedFrom(typeof(string));
            }, Is.Not.AllocatingGCMemory());
        }
    }
}
