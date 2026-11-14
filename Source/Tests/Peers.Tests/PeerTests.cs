using System.Buffers;
using Cards.Domain.Shared;
using Cards.Slices.CreateCard;
using Cards.Slices.MoveCard;
using Ecs.Contracts.Commands;
using Ecs.Domain;
using Ecs.Domain.Shared;
using Ecs.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Network.Domain;
using Peers.Handlers.Commands;
using Resources.Domain.Shared;
using Resources.Slices.ChangeResource;
using Testing;
using VSA.Contracts;
using Wire.Contracts.Commands;
using Wire.Handlers.Commands;

namespace Peers.Tests;

// A server sends its WireLog to each client. Each client applies the frames on its own bus, so the same handlers
// write each World and each client log equals the server log.
public sealed class PeerTests
{
    [Test]
    public async Task ServerAndTwoClients_CardsFlow_EqualWorldsAndLogs()
    {
        using PeerHost peers = PeerHost.Start(clients: 2);
        Peer server = peers.Server;

        EntityId card = (await server.Commands.SendAsync(new CreateCardCommand(Zone.Hand), default)).Value;
        await server.Commands.SendAsync(new MoveCardCommand(card, Zone.Board), default);
        Result<int> changed = await server.Commands.SendAsync(new ChangeResourceCommand<Health>(card, 5), default);
        await peers.IdleAsync();

        Assert.That(changed.Value, Is.EqualTo(5));
        IReadOnlyList<IMessage> serverLog = server.Services.GetRequiredService<WireLog>().Entries;
        Assert.That(serverLog, Has.Count.EqualTo(5));
        foreach (Peer client in peers.Clients)
        {
            IWorldReader world = client.Reader;
            Entity clientCard = world.ById(card);
            Assert.That(world.Count, Is.EqualTo(server.Reader.Count));
            Assert.That(world.HasTag<Card>(clientCard), Is.True);
            Assert.That(world.Get<CardZone>(clientCard).Value, Is.EqualTo(Zone.Board));
            Assert.That(world.Get<Health>(clientCard).Value, Is.EqualTo(5));
            Assert.That(client.Services.GetRequiredService<WireLog>().Entries, Is.EqualTo(serverLog));
        }
    }

    [Test]
    public async Task Server_SendsHelloFirst_ThenOneFrameForEachEntry_ToEachClient()
    {
        RecordingTransport first = new RecordingTransport();
        RecordingTransport second = new RecordingTransport();
        using Peer server = Peer.Start(PeerRole.Server, new ITransport[] { first, second });
        WireCatalog catalog = server.Services.GetRequiredService<WireCatalog>();

        EntityId id = (await server.Commands.SendAsync(new CreateEntity(default), default)).Value;

        Assert.That(first.Sent, Has.Count.EqualTo(2));
        Assert.That(catalog.Read(first.Sent[0]), Is.EqualTo(new WireHello(catalog.Hash)));
        Assert.That(catalog.Read(first.Sent[1]), Is.EqualTo(new CreateEntity(id)));
        Assert.That(second.Sent, Is.EqualTo(first.Sent));
    }

    [Test]
    public async Task Client_AppliesFrames_SendsNothing()
    {
        RecordingTransport toClient = new RecordingTransport();
        RecordingTransport toServer = new RecordingTransport();
        using Peer server = Peer.Start(PeerRole.Server, new ITransport[] { toClient });
        using Peer client = Peer.Start(PeerRole.Client, new ITransport[] { toServer });
        await server.Commands.SendAsync(new CreateCardCommand(Zone.Hand), default);

        foreach (byte[] frame in toClient.Sent)
        {
            toServer.Inject(frame);
        }

        await Receiver(client).Idle;

        Assert.That(client.Reader.Count, Is.EqualTo(1));
        Assert.That(toServer.Sent, Is.Empty);
    }

    [Test]
    public async Task Client_OtherKeys_FaultsBeforeAnyEntry()
    {
        InProcessTransport.Pair(out InProcessTransport serverSide, out InProcessTransport clientSide);
        using Peer client = Peer.Start(PeerRole.Client, new ITransport[] { clientSide });
        using Peer server = Peer.Start(
            PeerRole.Server,
            new ITransport[] { serverSide },
            s => s.AddSingleton(WireTypes.Of(t => t.Component<Extra>())));

        await server.Commands.SendAsync(new CreateCardCommand(Zone.Hand), default);

        Assert.ThrowsAsync<InvalidOperationException>(new Func<Task>(() => Receiver(client).Idle));
        Assert.That(client.Reader.Count, Is.Zero);
    }

    [Test]
    public void Client_EntryBeforeHello_Faults()
    {
        RecordingTransport toServer = new RecordingTransport();
        using Peer client = Peer.Start(PeerRole.Client, new ITransport[] { toServer });

        toServer.Inject(Frame(client, new CreateEntity(new EntityId(1))));

        Assert.ThrowsAsync<InvalidOperationException>(new Func<Task>(() => Receiver(client).Idle));
        Assert.That(client.Reader.Count, Is.Zero);
    }

    [Test]
    public void Client_EntryWithAnError_Faults_AndAppliesNoMoreFrames()
    {
        RecordingTransport toServer = new RecordingTransport();
        using Peer client = Peer.Start(PeerRole.Client, new ITransport[] { toServer });
        WireCatalog catalog = client.Services.GetRequiredService<WireCatalog>();

        toServer.Inject(Frame(client, new WireHello(catalog.Hash)));
        toServer.Inject(Frame(client, new CreateEntity(new EntityId(1))));
        toServer.Inject(Frame(client, new CreateEntity(new EntityId(1))));
        toServer.Inject(Frame(client, new CreateEntity(new EntityId(2))));

        Assert.ThrowsAsync<InvalidOperationException>(new Func<Task>(() => Receiver(client).Idle));
        Assert.That(client.Reader.Count, Is.EqualTo(1));
    }

    private static WireClient Receiver(Peer client)
    {
        return client.Services.GetRequiredService<WireClient>();
    }

    private static byte[] Frame(Peer peer, IMessage entry)
    {
        ArrayBufferWriter<byte> frame = new ArrayBufferWriter<byte>();
        peer.Services.GetRequiredService<WireCatalog>().Write(entry, frame);
        return frame.WrittenSpan.ToArray();
    }
}
