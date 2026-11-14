using System.Buffers;
using Ecs.Contracts.Commands;
using Ecs.Domain;
using Ecs.Domain.Shared;
using Microsoft.Extensions.Hosting;
using VSA.Contracts;
using Wire.Contracts.Commands;
using Wire.Tests.Fixtures;

namespace Wire.Tests;

// The server log goes to a client as frames. The client sends each primitive to its own bus, so the same
// handlers write its World and RecordStep fills its log. P8 does this over a transport.
public sealed class PeerTests
{
    [Test]
    public async Task Peers_FramesIntoClientBus_EqualWorldsAndLogs()
    {
        using IHost serverHost = Match.Host();
        using IHost clientHost = Match.Host();
        using Match server = new Match(serverHost);
        using Match client = new Match(clientHost);

        BlockId group = server.Groups.Open();
        EntityId card = (await server.Send(new CreateEntity(default))).Value;
        EntityId other = (await server.Send(new CreateEntity(default))).Value;
        await server.Send(new ChangeComponent<Health>(card, new Health { Value = 27 }));
        await server.Send(new ChangeComponent<Armor>(card, new Armor { Value = 3 }));
        await server.Send(new AddTag<Marked>(card));
        await server.Send(new ChangeComponent<Health>(card, new Health { Value = 20 }));
        await server.Send(new RemoveComponent<Armor>(card));
        await server.Send(new DeleteEntity(other));
        server.Groups.Close(group);

        foreach (IMessage entry in server.Log.Entries)
        {
            ArrayBufferWriter<byte> frame = new ArrayBufferWriter<byte>();
            server.Catalog.Write(entry, frame);
            IMessage received = client.Catalog.Read(frame.WrittenSpan);
            Error? error = await client.Catalog.ApplyAsync(received, client.Bus, client.Log, CancellationToken.None);
            Assert.That(error, Is.Null, entry.ToString());
        }

        Entity clientCard = client.World.ById(card);
        Assert.That(client.World.Count, Is.EqualTo(server.World.Count));
        Assert.That(client.World.Get<Health>(clientCard).Value, Is.EqualTo(20));
        Assert.That(client.World.Has<Armor>(clientCard), Is.False);
        Assert.That(client.World.HasTag<Marked>(clientCard), Is.True);
        Assert.That(client.World.TryById(other, out _), Is.False);
        Assert.That(client.Log.Entries, Is.EqualTo(server.Log.Entries));
    }
}
