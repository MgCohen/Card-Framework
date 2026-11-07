using Ecs.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using VSA.Host;

namespace Ecs.Tests;

// The module composes through AddVsa, as App/Host/Program.cs does.
public sealed class EcsModuleTests
{
    [Test]
    public void Module_RegistersOneWorldForEachProvider()
    {
        using WebApplication app = BuildApp();

        IWorldWriter writer = app.Services.GetRequiredService<IWorldWriter>();
        IWorldReader reader = app.Services.GetRequiredService<IWorldReader>();
        Entity entity = writer.Create();

        Assert.That(app.Services.GetRequiredService<IWorldWriter>(), Is.SameAs(writer));
        Assert.That(app.Services.GetRequiredService<IWorldReader>(), Is.SameAs(reader));
        Assert.That(reader.ById(entity.Id), Is.EqualTo(entity));
        Assert.That(reader, Is.Not.InstanceOf<IWorldWriter>());
    }

    [Test]
    public void Module_TwoProviders_HaveTwoWorlds()
    {
        using WebApplication first = BuildApp();
        using WebApplication second = BuildApp();

        first.Services.GetRequiredService<IWorldWriter>().Create();

        Assert.That(second.Services.GetRequiredService<IWorldReader>().Count, Is.Zero);
    }

    [Test]
    public void Module_DoesNotRegisterTheWorldType()
    {
        using WebApplication app = BuildApp();

        Assert.That(app.Services.GetService<World>(), Is.Null);
    }

    private static WebApplication BuildApp()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.AddVsa();
        return builder.Build();
    }
}
