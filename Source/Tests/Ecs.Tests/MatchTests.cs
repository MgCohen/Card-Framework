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
        Assert.Throws<ObjectDisposedException>(new Action(() => Reader(match)));
    }

    [Test]
    public void Dispose_ReaderFromBefore_Throws()
    {
        IServiceScope match = NewMatch();
        IWorldReader reader = Reader(match);

        match.Dispose();

        Assert.Throws<ObjectDisposedException>(new Action(() => _ = reader.Count));
        Assert.Throws<ObjectDisposedException>(new Action(() => reader.TryById(new EntityId(1), out _)));
    }

    [Test]
    public void Dispose_WriterFromBefore_Throws()
    {
        IServiceScope match = NewMatch();
        IWorldWriter writer = match.ServiceProvider.GetRequiredService<MatchWorld>().World.Writer();

        match.Dispose();

        Assert.Throws<ObjectDisposedException>(new Action(() => writer.Create()));
    }

    [Test]
    public void Create_WorldFactoryThrows_DisposesTheScope()
    {
        DisposeLog log = new DisposeLog();
        ServiceCollection services = new ServiceCollection();
        services.AddSingleton(log);
        services.AddScoped<ScopeProbe>();
        services.AddScoped(provider =>
        {
            provider.GetRequiredService<ScopeProbe>();
            return new MatchWorld();
        });
        using ServiceProvider provider = services.BuildServiceProvider();
        MatchFactory factory = new MatchFactory(provider.GetRequiredService<IServiceScopeFactory>(), new FailingWorldFactory());

        Assert.Throws<InvalidOperationException>(new Action(() => factory.Create()));
        Assert.That(log.Count, Is.EqualTo(1));
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

public sealed class DisposeLog
{
    public int Count { get; set; }
}

public sealed class ScopeProbe : IDisposable
{
    private readonly DisposeLog log;

    public ScopeProbe(DisposeLog log)
    {
        this.log = log;
    }

    public void Dispose()
    {
        log.Count++;
    }
}

public sealed class FailingWorldFactory : IWorldFactory
{
    public World Create()
    {
        throw new InvalidOperationException("The test factory makes no World.");
    }
}
