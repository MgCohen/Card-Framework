using System.Buffers;
using Ecs.Contracts.Commands;
using Ecs.Domain;
using Ecs.Domain.Shared;
using Microsoft.Extensions.Hosting;
using VSA.Contracts;
using Wire.Handlers.Commands;
using Wire.Tests.Fixtures;

namespace Wire.Tests;

public sealed class ManagedComponentTests
{
    private IHost host = null!;
    private Match match = null!;

    [SetUp]
    public void Start()
    {
        host = Match.Host();
        match = new Match(host);
    }

    [TearDown]
    public void Stop()
    {
        match?.Dispose();
        host?.Dispose();
    }

    [Test]
    public async Task ChangeComponent_StringComponent_WritesAndReturnsValue()
    {
        Entity entity = match.World.Create();

        Result<CardName> result = await match.Send(new ChangeComponent<CardName>(entity.Id, new CardName { Value = "Goblin" }));

        Assert.That(result.IsSuccess, Is.True, result.Error?.Message);
        Assert.That(match.World.Get<CardName>(entity).Value, Is.EqualTo("Goblin"));
    }

    [Test]
    public async Task ChangeComponent_EqualString_ReturnsUnchanged()
    {
        Entity entity = match.World.Create();
        match.World.Set(entity, new CardName { Value = new string('G', 1) + "oblin" });

        Result<CardName> result = await match.Send(new ChangeComponent<CardName>(entity.Id, new CardName { Value = "Goblin" }));

        Assert.That(result.Error, Is.EqualTo(PrimitiveErrors.Unchanged));
    }

    [Test]
    public void Catalog_StringComponent_RoundTripsThroughMemoryPack()
    {
        WireCatalog catalog = new WireCatalog(new[] { Match.Keys() });
        ArrayBufferWriter<byte> frame = new ArrayBufferWriter<byte>();

        catalog.Write(new ChangeComponent<CardName>(new EntityId(3), new CardName { Value = "Goblin" }), frame);
        ChangeComponent<CardName> read = (ChangeComponent<CardName>)catalog.Read(frame.WrittenSpan);

        Assert.That(read.Value.Value, Is.EqualTo("Goblin"));
        Assert.That(read.Entity, Is.EqualTo(new EntityId(3)));
    }
}
