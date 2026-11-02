using NUnit.Framework;
using VContainer;

namespace Framework.Routing.Tests
{
    public class InstallTests
    {
        [Test]
        public void AddRouting_Only_ResolvesPublicApi()
        {
            var builder = new ContainerBuilder();
            builder.AddRouting(o =>
            {
                o.IncludeTestAssemblies = true;
                o.Route<INoteReader>(RouteMode.Single);
            });

            var router = builder.Build().Resolve<TypeRouter<INoteReader>>();

            Assert.That(router.Get(typeof(Hello)), Is.InstanceOf<HelloReader>());
            Assert.That(router.RoutedTypes, Is.EquivalentTo(new[] { typeof(Hello), typeof(Bye), typeof(Gen<int>) }));
        }

        [Test]
        public void AddRouting_OptionOrder_DoesNotMatter()
        {
            var builder = new ContainerBuilder();
            builder.AddRouting(o =>
            {
                o.Route<INoteReader>(RouteMode.Single);
                o.IncludeTestAssemblies = true;
            });

            Assert.That(builder.Build().Resolve<TypeRouter<INoteReader>>().RoutedTypes, Has.Count.EqualTo(3));
        }

        [Test]
        public void AddRouting_ScanOff_RegistersEmptyRouter()
        {
            var builder = new ContainerBuilder();
            builder.AddRouting(o =>
            {
                o.Scan = false;
                o.IncludeTestAssemblies = true;
                o.Route<INoteReader>(RouteMode.Multiple);
            });

            Assert.That(builder.Build().Resolve<TypeRouter<INoteReader>>().RoutedTypes, Is.Empty);
        }
    }
}
