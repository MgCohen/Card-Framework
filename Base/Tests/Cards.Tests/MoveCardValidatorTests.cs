using Cards.Domain.Shared;
using Cards.Slices.CreateCard;
using Cards.Slices.MoveCard;
using Ecs.Contracts.Commands;
using Ecs.Domain.Shared;
using Ecs.Handlers.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VSA.Composition;
using VSA.Contracts;
using Wire.Handlers.Commands;

namespace Cards.Tests;

// The first module validator. The host scan finds it in Cards.Handlers.Commands: no test code registers it.
// A rejected move gives an error, and no primitive goes to the log.
public sealed class MoveCardValidatorTests
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
    public async Task UnknownCard_IsRejected()
    {
        await AssertRejected(new MoveCardCommand(new EntityId(99), Zone.Board), CardErrors.NotACard);
    }

    [Test]
    public async Task EntityWithNoCardTag_IsRejected()
    {
        EntityId plain = (await match.Commands.SendAsync(new CreateEntity(default), CancellationToken.None)).Value;

        await AssertRejected(new MoveCardCommand(plain, Zone.Board), CardErrors.NotACard);
    }

    [Test]
    public async Task MoveToTheSameZone_IsRejected()
    {
        CreateCardCommand create = new CreateCardCommand(Zone.Hand);
        EntityId card = (await match.Commands.SendAsync(create, CancellationToken.None)).Value;

        await AssertRejected(new MoveCardCommand(card, Zone.Hand), CardErrors.AlreadyThere);
    }

    private async Task AssertRejected(MoveCardCommand move, Error expected)
    {
        WireLog log = match.Services.GetRequiredService<WireLog>();
        int before = log.Entries.Count;

        Result<Unit> result = await match.Commands.SendAsync(move, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(expected));
        Assert.That(result.Envelope?.State, Is.EqualTo(EnvelopeState.Rejected));
        Assert.That(log.Entries, Has.Count.EqualTo(before));
    }
}
