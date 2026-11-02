using NUnit.Framework;
using VContainer;

namespace Framework.TypeRouting.Tests
{
    public class InstallTests
    {
        [Test]
        public void AddTypeRouting_Only_ResolvesPublicApi()
        {
            var builder = new ContainerBuilder();
            builder.AddTypeRouting(o =>
            {
                o.IncludeTestAssemblies = true;
                o.Route<INoteReader>(RouteMode.Single);
            });

            var router = builder.Build().Resolve<TypeRouter<INoteReader>>();

            Assert.That(router.Get(typeof(Hello)), Is.InstanceOf<HelloReader>());
            Assert.That(router.RoutedTypes, Is.EquivalentTo(new[] { typeof(Hello), typeof(Bye), typeof(Gen<int>) }));
        }

        [Test]
        public void AddTypeRouting_OptionOrder_DoesNotMatter()
        {
            var builder = new ContainerBuilder();
            builder.AddTypeRouting(o =>
            {
                o.Route<INoteReader>(RouteMode.Single);
                o.IncludeTestAssemblies = true;
            });

            Assert.That(builder.Build().Resolve<TypeRouter<INoteReader>>().RoutedTypes, Has.Count.EqualTo(3));
        }

        [Test]
        public void AddTypeRouting_ScanOff_RegistersEmptyRouter()
        {
            var builder = new ContainerBuilder();
            builder.AddTypeRouting(o =>
            {
                o.Scan = false;
                o.IncludeTestAssemblies = true;
                o.Route<INoteReader>(RouteMode.Multiple);
            });

            Assert.That(builder.Build().Resolve<TypeRouter<INoteReader>>().RoutedTypes, Is.Empty);
        }
    }
}
