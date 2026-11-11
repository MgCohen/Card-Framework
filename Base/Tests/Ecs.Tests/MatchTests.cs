using Ecs.Contracts.Commands;
using Ecs.Domain;
using Ecs.Domain.Shared;
using Ecs.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VSA.Composition;
using VSA.Contracts;
using VSA.Contracts.Commands;

namespace Ecs.Tests;

// MatchFactory makes one scope with one World. The scope gives the reader to gameplay code. No service gives the World
// or the writer; only the primitive handlers reach it, through MatchWorld.
public sealed class MatchTests
{
    private IHost host = null!;

    [SetUp]
    public void SetUp()
    {
        host = VsaBuilder.Create().UseGenericHost().Build();
    }

    [TearDown]
    public void TearDown()
    {
        host.Dispose();
    }

    [Test]
    public async Task Match_SendsPrimitivesToItsOwnWorld()
    {
        using IServiceScope first = NewMatch();
        using IServiceScope second = NewMatch();

        Result<EntityId> created = await Bus(first).SendAsync(new CreateEntity(default), CancellationToken.None);

        Assert.That(created.IsSuccess, Is.True);
        Assert.That(Reader(first).TryById(created.Value, out _), Is.True);
        Assert.That(Reader(first).Count, Is.EqualTo(1));
        Assert.That(Reader(second).Count, Is.Zero);
    }

    [Test]
    public async Task MatchScope_GivesTheReaderOfTheMatchWorld()
    {
        using IServiceScope match = NewMatch();
        EntityId id = (await Bus(match).SendAsync(new CreateEntity(default), CancellationToken.None)).Value;

        IWorldReader reader = Reader(match);

        Assert.That(reader.TryById(id, out _), Is.True);
        Assert.That(reader, Is.Not.InstanceOf<IWorldWriter>());
    }

    [Test]
    public void MatchScope_GivesNoWorldAndNoWriter()
    {
        using IServiceScope match = NewMatch();

        Assert.That(match.ServiceProvider.GetService<World>(), Is.Null);
        Assert.That(match.ServiceProvider.GetService<IWorldWriter>(), Is.Null);
    }

    [Test]
    public void ScopeWithNoMatch_ReaderThrows()
    {
        using IServiceScope scope = host.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        Assert.Throws<InvalidOperationException>(new Action(() => services.GetRequiredService<IWorldReader>()));
        Assert.Throws<InvalidOperationException>(new Action(() => host.Services.GetRequiredService<IWorldReader>()));
    }

    [Test]
    public void ScopeWithNoMatch_PrimitiveThrows()
    {
        using IServiceScope scope = host.Services.CreateScope();
        ICommandSender bus = scope.ServiceProvider.GetRequiredService<ICommandSender>();

        Assert.ThrowsAsync<InvalidOperationException>(
            new Func<Task>(() => bus.SendAsync(new CreateEntity(default), CancellationToken.None)));
    }

    [Test]
    public void MatchWorld_BindsOneWorldOnly()
    {
        using IServiceScope match = NewMatch();
        MatchWorld world = match.ServiceProvider.GetRequiredService<MatchWorld>();

        World other = host.Services.GetRequiredService<IWorldFactory>().Create();

        Assert.Throws<InvalidOperationException>(new Action(() => world.Bind(other)));
    }

    [Test]
    public void Dispose_EndsTheScope()
    {
        IServiceScope match = NewMatch();

        match.Dispose();

        Assert.Throws<ObjectDisposedException>(new Action(() => Bus(match)));
    }

    private static ICommandSender Bus(IServiceScope match)
    {
        return match.ServiceProvider.GetRequiredService<ICommandSender>();
    }

    private static IWorldReader Reader(IServiceScope match)
    {
        return match.ServiceProvider.GetRequiredService<IWorldReader>();
    }

    private IServiceScope NewMatch()
    {
        return host.Services.GetRequiredService<MatchFactory>().Create();
    }
}
