using Cards.Domain.Shared;
using Cards.Slices.CreateCard;
using Cards.Slices.MoveCard;
using Cards.Slices.PlayCard;
using Ecs.Contracts.Commands;
using Ecs.Domain;
using Ecs.Domain.Shared;
using Ecs.Handlers.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Resources.Domain.Shared;
using Resources.Slices.ChangeResource;
using VSA.Contracts;
using VSA.Composition;
using Wire.Handlers.Commands;

namespace Cards.Tests;

// PlayCard moves a card from the hand to the board, then pays its cost in mana. It sends MoveCard and the
// Resources ChangeResource on the bus. The validator checks the card and the mana first, because no step rolls back.
public sealed class PlayCardTests
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
    public async Task Play_MovesTheCardToTheBoard_AndPaysTheCost()
    {
        EntityId card = await Card(Zone.Hand, 3);
        EntityId player = await Player(10);
        WireLog log = match.Services.GetRequiredService<WireLog>();
        int before = log.Entries.Count;

        Result<Unit> result = await Send(new PlayCardCommand(card, player));

        IWorldReader world = match.Reader;
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(world.Get<CardZone>(world.ById(card)).Value, Is.EqualTo(Zone.Board));
        Assert.That(world.Get<Mana>(world.ById(player)).Value, Is.EqualTo(7));
        Assert.That(log.Entries.Skip(before), Is.EqualTo(new IMessage[]
        {
            new ChangeComponent<CardZone>(card, new CardZone { Value = Zone.Board }),
            new ChangeComponent<Mana>(player, new Mana { Value = 7 }),
        }));
    }

    [Test]
    public async Task ACardWithNoCost_MovesAndPaysNothing()
    {
        EntityId card = await Card(Zone.Hand, 0);
        EntityId player = await Player(10);
        WireLog log = match.Services.GetRequiredService<WireLog>();
        int before = log.Entries.Count;

        Result<Unit> result = await Send(new PlayCardCommand(card, player));

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(match.Reader.Get<Mana>(match.Reader.ById(player)).Value, Is.EqualTo(10));
        Assert.That(log.Entries, Has.Count.EqualTo(before + 1));
    }

    [Test]
    public async Task ACardOutOfTheHand_IsRejected()
    {
        EntityId card = await Card(Zone.Deck, 1);
        EntityId player = await Player(10);

        await AssertRejected(new PlayCardCommand(card, player), PlayCardErrors.NotInHand);
    }

    [Test]
    public async Task NotEnoughMana_IsRejected()
    {
        EntityId card = await Card(Zone.Hand, 9);
        EntityId player = await Player(8);

        await AssertRejected(new PlayCardCommand(card, player), PlayCardErrors.NotEnoughMana);
    }

    [Test]
    public async Task APlayerWithNoMana_IsRejected()
    {
        EntityId card = await Card(Zone.Hand, 1);
        EntityId player = (await match.Commands.SendAsync(new CreateEntity(default), CancellationToken.None)).Value;

        await AssertRejected(new PlayCardCommand(card, player), PlayCardErrors.NotEnoughMana);
    }

    [Test]
    public async Task AFreeCardWithNoPlayer_IsRejected()
    {
        EntityId card = await Card(Zone.Hand, 0);

        await AssertRejected(new PlayCardCommand(card, new EntityId(999)), PlayCardErrors.NoPlayer);
    }

    [Test]
    public async Task ANegativeCost_IsRejected()
    {
        CreateCardCommand create = new CreateCardCommand(Zone.Hand, -5);

        Result<EntityId> result = await match.Commands.SendAsync(create, CancellationToken.None);

        Assert.That(result.Error, Is.EqualTo(CreateCardErrors.NegativeCost));
    }

    [Test]
    public async Task NotACard_IsRejected()
    {
        EntityId player = await Player(10);

        await AssertRejected(new PlayCardCommand(player, player), CardErrors.NotACard);
    }

    [Test]
    public async Task CreateCard_WritesTheCost()
    {
        EntityId card = await Card(Zone.Hand, 4);

        Assert.That(match.Reader.Get<Cost>(match.Reader.ById(card)).Value, Is.EqualTo(4));
    }

    private async Task<EntityId> Card(Zone zone, int cost)
    {
        return (await match.Commands.SendAsync(new CreateCardCommand(zone, cost), CancellationToken.None)).Value;
    }

    private async Task<EntityId> Player(int mana)
    {
        EntityId player = (await match.Commands.SendAsync(new CreateEntity(default), CancellationToken.None)).Value;
        await match.Commands.SendAsync(new ChangeResourceCommand(player, mana, Resource.Mana), CancellationToken.None);
        return player;
    }

    private Task<Result<Unit>> Send(PlayCardCommand play)
    {
        return match.Commands.SendAsync(play, CancellationToken.None);
    }

    private async Task AssertRejected(PlayCardCommand play, Error expected)
    {
        WireLog log = match.Services.GetRequiredService<WireLog>();
        int before = log.Entries.Count;

        Result<Unit> result = await Send(play);

        Assert.That(result.Error, Is.EqualTo(expected));
        Assert.That(result.Envelope?.State, Is.EqualTo(EnvelopeState.Rejected));
        Assert.That(log.Entries, Has.Count.EqualTo(before));
    }
}
