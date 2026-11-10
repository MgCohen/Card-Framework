using Ecs.Contracts.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VSA.Composition;
using VSA.Handlers;
using Wire.Handlers.Commands;
using Wire.Tests.Fixtures;

namespace Wire.Tests;

public sealed class WirePluginTests
{
    [Test]
    public void Plugin_HostStarts_WithOpenPrimitiveHandlers()
    {
        Assert.DoesNotThrow(new Action(() =>
        {
            using IHost host = VsaBuilder.Create().UseGenericHost().Build();
        }));
    }

    [Test]
    public void Plugin_Catalog_HasTheTypesOfEachRegistration()
    {
        using IHost host = VsaBuilder.Create().UseGenericHost().Services(services =>
        {
            services.AddSingleton(WireTypes.Of(t => t.Component<Health>()));
            services.AddSingleton(WireTypes.Of(t => t.Tag<Marked>()));
        }).Build();

        WireCatalog catalog = host.Services.GetRequiredService<WireCatalog>();

        Assert.That(catalog.Count, Is.EqualTo(8));
        Assert.That(host.Services.GetRequiredService<WireCatalog>(), Is.SameAs(catalog));
    }

    [Test]
    public void Plugin_Log_OneForEachScope()
    {
        using IHost host = Match.Host();
        using IServiceScope first = host.Services.CreateScope();
        using IServiceScope second = host.Services.CreateScope();

        WireLog log = first.ServiceProvider.GetRequiredService<WireLog>();

        Assert.That(first.ServiceProvider.GetRequiredService<WireLog>(), Is.SameAs(log));
        Assert.That(second.ServiceProvider.GetRequiredService<WireLog>(), Is.Not.SameAs(log));
    }

    [Test]
    public void Plugin_Pipeline_IsRegisteredForPrimitives()
    {
        using IHost host = Match.Host();

        Assert.That(host.Services.GetService<IPipeline<IPrimitive>>(), Is.Not.Null);
    }
}
