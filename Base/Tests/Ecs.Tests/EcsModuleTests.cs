using Ecs.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VSA.Composition;

namespace Ecs.Tests;

// The plugin composes through VsaBuilder, as App/Host/Program.cs does. The container gives the factory only:
// the owner of a match makes its World and gives access by hand.
public sealed class EcsModuleTests
{
    [Test]
    public void Module_RegistersOneWorldFactory()
    {
        using IHost app = BuildApp();

        IWorldFactory factory = app.Services.GetRequiredService<IWorldFactory>();

        Assert.That(app.Services.GetRequiredService<IWorldFactory>(), Is.SameAs(factory));
    }

    [Test]
    public void Module_RegistersNoWorldReaderOrWriter()
    {
        using IHost app = BuildApp();

        Assert.That(app.Services.GetService<World>(), Is.Null);
        Assert.That(app.Services.GetService<IWorldWriter>(), Is.Null);
        Assert.That(app.Services.GetService<IWorldReader>(), Is.Null);
    }

    [Test]
    public void Factory_MakesANewWorldEachTime()
    {
        using IHost app = BuildApp();
        IWorldFactory factory = app.Services.GetRequiredService<IWorldFactory>();

        World first = factory.Create();
        World second = factory.Create();
        first.Create();

        Assert.That(second, Is.Not.SameAs(first));
        Assert.That(second.Count, Is.Zero);
    }

    private static IHost BuildApp()
    {
        return VsaBuilder.Create().UseGenericHost().Build();
    }
}
