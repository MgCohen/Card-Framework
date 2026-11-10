using Ecs.Contracts.Commands;
using Ecs.Domain;
using Ecs.Domain.Shared;
using Ecs.Handlers.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VSA.Composition;
using VSA.Contracts;
using VSA.Contracts.Commands;

namespace Ecs.Tests;

// One Match owns one World and one scope. The scope gives the reader to gameplay code. No service gives the World or
// the writer; only the primitive handlers reach it, through MatchWorld.
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
        using Match first = new Match(host.Services);
        using Match second = new Match(host.Services);

        Result<EntityId> created = await first.Commands.SendAsync(new CreateEntity(default), CancellationToken.None);

        Assert.That(created.IsSuccess, Is.True);
        Assert.That(first.Reader.TryById(created.Value, out _), Is.True);
        Assert.That(first.Reader.Count, Is.EqualTo(1));
        Assert.That(second.Reader.Count, Is.Zero);
    }

    [Test]
    public async Task MatchScope_GivesTheReaderOfTheMatchWorld()
    {
        using Match match = new Match(host.Services);
        EntityId id = (await match.Commands.SendAsync(new CreateEntity(default), CancellationToken.None)).Value;

        IWorldReader reader = match.Services.GetRequiredService<IWorldReader>();

        Assert.That(reader.TryById(id, out _), Is.True);
        Assert.That(reader, Is.Not.InstanceOf<IWorldWriter>());
        Assert.That(match.Reader, Is.Not.InstanceOf<IWorldWriter>());
    }

    [Test]
    public void MatchScope_GivesNoWorldAndNoWriter()
    {
        using Match match = new Match(host.Services);

        Assert.That(match.Services.GetService<World>(), Is.Null);
        Assert.That(match.Services.GetService<IWorldWriter>(), Is.Null);
    }

    [Test]
    public void ScopeWithNoMatch_ReaderThrows()
    {
        using IServiceScope scope = host.Services.CreateScope();

        Assert.Throws<InvalidOperationException>(new Action(() => scope.ServiceProvider.GetRequiredService<IWorldReader>()));
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
        using Match match = new Match(host.Services);
        MatchWorld world = match.Services.GetRequiredService<MatchWorld>();

        Assert.Throws<InvalidOperationException>(new Action(() => world.Bind(match.World)));
    }

    [Test]
    public void Dispose_EndsTheScope()
    {
        Match match = new Match(host.Services);

        match.Dispose();

        Assert.Throws<ObjectDisposedException>(new Action(() => _ = match.Commands));
    }
}
