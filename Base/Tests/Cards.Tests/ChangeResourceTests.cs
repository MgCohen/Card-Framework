using Ecs.Contracts.Commands;
using Ecs.Domain.Shared;
using Ecs.Domain;
using Ecs.Handlers.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Resources.Domain.Shared;
using Resources.Slices.ChangeResource;
using VSA.Composition;
using VSA.Contracts.Commands;
using VSA.Contracts;
using Wire.Handlers.Commands;

namespace Cards.Tests;

// ChangeResource is in the Resources module, not in Cards. It works on each live entity, not only on cards.
public sealed class ChangeResourceTests
{
    private IHost host = null!;
    private IServiceScope match = null!;

    [SetUp]
    public void SetUp()
    {
        host = VsaBuilder.Create().UseGenericHost().Build();
        match = host.Services.GetRequiredService<MatchFactory>().Create();
    }

    [TearDown]
    public void TearDown()
    {
        match.Dispose();
        host.Dispose();
    }

    [Test]
    public async Task EntityWithNoHealth_StartsAtZero()
    {
        EntityId target = (await Commands.SendAsync(new CreateEntity(default), CancellationToken.None)).Value;

        ChangeResourceCommand change = new ChangeResourceCommand(target, -4);

        Result<int> result = await Commands.SendAsync(change, CancellationToken.None);

        Assert.That(result.Value, Is.EqualTo(-4));
        Assert.That(Reader.Get<Health>(Reader.ById(target)).Value, Is.EqualTo(-4));
    }

    [Test]
    public async Task ZeroDelta_GivesUnchanged_AndAddsNoHealth()
    {
        EntityId target = (await Commands.SendAsync(new CreateEntity(default), CancellationToken.None)).Value;
        WireLog log = match.ServiceProvider.GetRequiredService<WireLog>();

        ChangeResourceCommand change = new ChangeResourceCommand(target, 0);

        Result<int> result = await Commands.SendAsync(change, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(PrimitiveErrors.Unchanged));
        Assert.That(Reader.Has<Health>(Reader.ById(target)), Is.False);
        Assert.That(log.Entries, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task UnknownTarget_GivesEntityMissing()
    {
        WireLog log = match.ServiceProvider.GetRequiredService<WireLog>();
        ChangeResourceCommand change = new ChangeResourceCommand(new EntityId(99), 1);

        Result<int> result = await Commands.SendAsync(change, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(PrimitiveErrors.EntityMissing));
        Assert.That(log.Entries, Is.Empty);
    }

    private ICommandSender Commands => match.ServiceProvider.GetRequiredService<ICommandSender>();

    private IWorldReader Reader => match.ServiceProvider.GetRequiredService<IWorldReader>();
}
