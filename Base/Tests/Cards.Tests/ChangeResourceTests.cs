using Ecs.Contracts.Commands;
using Ecs.Domain.Shared;
using Ecs.Handlers.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Resources.Domain.Shared;
using Resources.Slices.ChangeResource;
using VSA.Composition;
using VSA.Contracts;
using Wire.Handlers.Commands;

namespace Cards.Tests;

// ChangeResource is in the Resources module, not in Cards. It works on each live entity, not only on cards.
public sealed class ChangeResourceTests
{
    private IHost host = null!;
    private Match match = null!;

    [SetUp]
    public void SetUp()
    {
        host = VsaBuilder.Create().UseGenericHost().Build();
        match = new Match(host.Services);
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
        EntityId target = (await match.Commands.SendAsync(new CreateEntity(default), CancellationToken.None)).Value;

        ChangeResourceCommand change = new ChangeResourceCommand(target, -4);

        Result<int> result = await match.Commands.SendAsync(change, CancellationToken.None);

        Assert.That(result.Value, Is.EqualTo(-4));
        Assert.That(match.Reader.Get<Health>(match.Reader.ById(target)).Value, Is.EqualTo(-4));
    }

    [Test]
    public async Task ZeroDelta_GivesUnchanged_AndAddsNoHealth()
    {
        EntityId target = (await match.Commands.SendAsync(new CreateEntity(default), CancellationToken.None)).Value;
        WireLog log = match.Services.GetRequiredService<WireLog>();

        ChangeResourceCommand change = new ChangeResourceCommand(target, 0);

        Result<int> result = await match.Commands.SendAsync(change, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(PrimitiveErrors.Unchanged));
        Assert.That(match.Reader.Has<Health>(match.Reader.ById(target)), Is.False);
        Assert.That(log.Entries, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task UnknownTarget_GivesEntityMissing()
    {
        WireLog log = match.Services.GetRequiredService<WireLog>();
        ChangeResourceCommand change = new ChangeResourceCommand(new EntityId(99), 1);

        Result<int> result = await match.Commands.SendAsync(change, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(PrimitiveErrors.EntityMissing));
        Assert.That(log.Entries, Is.Empty);
    }
}
