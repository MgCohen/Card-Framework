using NUnit.Framework;
using VContainer;

namespace Framework.TypeRouting.Tests
{
    public class RegistrationTests
    {
        [Test]
        public void NoScan_ExplicitMember_ResolvesRouter()
        {
            var builder = new ContainerBuilder();
            builder.Register<HelloReader>(Lifetime.Singleton).As<INoteReader>();
            builder.RegisterTypeRouter<INoteReader>(RouteMode.Single, scan: false);

            var router = builder.Build().Resolve<TypeRouter<INoteReader>>();

            Assert.That(router.RoutedTypes, Is.EqualTo(new[] { typeof(Hello) }));
        }

        [Test]
        public void Scan_IncludeTests_RegistersEachFixtureReader()
        {
            var builder = new ContainerBuilder();
            builder.RegisterTypeRouter<INoteReader>(RouteMode.Single, includeTests: true);

            var router = builder.Build().Resolve<TypeRouter<INoteReader>>();

            Assert.That(router.RoutedTypes, Is.EquivalentTo(new[] { typeof(Hello), typeof(Bye), typeof(Gen<int>) }));
        }

        [Test]
        public void Scan_Default_EmptyRouter_Resolves()
        {
            var builder = new ContainerBuilder();
            builder.RegisterTypeRouter<INoteReader>(RouteMode.Single);

            Assert.That(builder.Build().Resolve<TypeRouter<INoteReader>>().RoutedTypes, Is.Empty);
        }
    }
}
