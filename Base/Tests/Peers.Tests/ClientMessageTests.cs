using System.Buffers;
using Cards.Domain.Shared;
using Cards.Slices.CreateCard;
using Ecs.Contracts.Commands;
using Ecs.Domain.Shared;
using MemoryPack;
using Microsoft.Extensions.DependencyInjection;
using Network.Domain;
using Peers.Contracts.Commands;
using Peers.Handlers.Commands;
using Peers.Tests.Fixtures;
using Testing;
using VSA.Contracts;
using Wire.Contracts.Commands;
using Wire.Handlers.Commands;

namespace Peers.Tests;

// A client sends a message to the server. The server runs it on its match bus and sends the result back to that client
// only. The handler of the message type selects the path: a reply only, or primitives that reach each client.
public sealed class ClientMessageTests
{
    [Test]
    public async Task Ping_GivesPong()
    {
        using PeerHost peers = PeerHost.Start(clients: 1);

        Result<Pong> pong = await peers.Clients[0].WireClient.SendAsync(new Ping(7), default);

        Assert.That(pong.IsSuccess, Is.True);
        Assert.That(pong.Value, Is.EqualTo(new Pong(7)));
    }

    [Test]
    public async Task Echo_GivesTheValue()
    {
        using PeerHost peers = PeerHost.Start(clients: 1, TestCallHandler.Register);

        Result<int> echo = await peers.Clients[0].WireClient.SendAsync(new TestCall(CallMode.Echo, 42), default);

        Assert.That(echo.Value, Is.EqualTo(42));
    }

    [Test]
    public async Task Spawn_EntriesReachEachClient_BeforeTheReply()
    {
        using PeerHost peers = PeerHost.Start(clients: 2, TestCallHandler.Register);
        Peer sender = peers.Clients[0];

        Result<int> spawned = await sender.WireClient.SendAsync(new TestCall(CallMode.Spawn, 0), default);

        EntityId id = new EntityId(spawned.Value);
        Assert.That(sender.Reader.TryById(id, out _), Is.True);
        await peers.IdleAsync();
        Assert.That(peers.Server.Reader.TryById(id, out _), Is.True);
        Assert.That(peers.Clients[1].Reader.TryById(id, out _), Is.True);
    }

    [Test]
    public async Task Refuse_GivesTheError_AndTheMatchContinues()
    {
        using PeerHost peers = PeerHost.Start(clients: 1, TestCallHandler.Register);
        WireClient client = peers.Clients[0].WireClient;

        Result<int> refused = await client.SendAsync(new TestCall(CallMode.Refuse, 0), default);
        Result<Pong> pong = await client.SendAsync(new Ping(1), default);

        Assert.That(refused.IsSuccess, Is.False);
        Assert.That(refused.Error!.Code, Is.EqualTo(TestCallHandler.Refused.Code));
        Assert.That(pong.Value, Is.EqualTo(new Pong(1)));
    }

    [Test]
    public async Task Throw_GivesHandlerFailed_AndTheMatchContinues()
    {
        using PeerHost peers = PeerHost.Start(clients: 1, TestCallHandler.Register);
        WireClient client = peers.Clients[0].WireClient;

        Result<int> failed = await client.SendAsync(new TestCall(CallMode.Throw, 0), default);
        Result<Pong> pong = await client.SendAsync(new Ping(2), default);

        Assert.That(failed.Error!.Code, Is.EqualTo(PeerErrors.HandlerFailed.Code));
        Assert.That(pong.Value, Is.EqualTo(new Pong(2)));
        Assert.That(peers.Server.WireServer.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Client_SendsAMessageFrame_RepliesCompleteInSendOrder()
    {
        RecordingTransport toServer = new RecordingTransport();
        using Peer client = Peer.Start(PeerRole.Client, new ITransport[] { toServer });
        WireCatalog catalog = client.Services.GetRequiredService<WireCatalog>();
        toServer.Inject(Frame(catalog, new WireHello(catalog.Hash)));

        Task<Result<Pong>> first = client.WireClient.SendAsync(new Ping(1), default);
        Task<Result<Pong>> second = client.WireClient.SendAsync(new Ping(2), default);
        toServer.Inject(Frame(catalog, Reply(new Pong(1))));
        toServer.Inject(Frame(catalog, Reply(new Pong(2))));

        Assert.That(catalog.Read(toServer.Sent[0]), Is.EqualTo(new Ping(1)));
        Assert.That(catalog.Read(toServer.Sent[1]), Is.EqualTo(new Ping(2)));
        Assert.That((await first).Value, Is.EqualTo(new Pong(1)));
        Assert.That((await second).Value, Is.EqualTo(new Pong(2)));
    }

    [Test]
    public void Client_Dispose_FailsTheOpenSends()
    {
        RecordingTransport toServer = new RecordingTransport();
        Peer client = Peer.Start(PeerRole.Client, new ITransport[] { toServer });
        Task<Result<Pong>> open = client.WireClient.SendAsync(new Ping(1), default);

        client.Dispose();

        Assert.ThrowsAsync<ObjectDisposedException>(new Func<Task>(() => open));
    }

    [Test]
    public async Task Server_PrimitiveFrameFromAClient_DisconnectsIt()
    {
        RecordingTransport toClient = new RecordingTransport();
        using Peer server = Peer.Start(PeerRole.Server, new ITransport[] { toClient });
        WireCatalog catalog = server.Services.GetRequiredService<WireCatalog>();

        toClient.Inject(Frame(catalog, new CreateEntity(new EntityId(1))));
        await server.Commands.SendAsync(new CreateCardCommand(Zone.Hand), default);

        Assert.That(server.WireServer.Count, Is.Zero);
        Assert.That(server.Reader.Count, Is.EqualTo(1));
        Assert.That(toClient.Sent, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Disconnect_TheOtherClientGetsTheNextEntries()
    {
        using PeerHost peers = PeerHost.Start(clients: 2);
        Peer gone = peers.Clients[0];
        Peer stays = peers.Clients[1];

        peers.Disconnect(gone);
        peers.Disconnect(gone);
        await peers.Server.Commands.SendAsync(new CreateCardCommand(Zone.Hand), default);
        await peers.IdleAsync();

        Assert.That(peers.Server.WireServer.Count, Is.EqualTo(1));
        Assert.That(gone.Reader.Count, Is.Zero);
        Assert.That(stays.Reader.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Client_FramesFromTwoThreads_ApplyOneAtATime_InTheOrderOfEachThread()
    {
        const int changes = 200;
        RecordingTransport toServer = new RecordingTransport();
        using Peer client = Peer.Start(PeerRole.Client, new ITransport[] { toServer });
        WireCatalog catalog = client.Services.GetRequiredService<WireCatalog>();
        toServer.Inject(Frame(catalog, new WireHello(catalog.Hash)));
        toServer.Inject(Frame(catalog, new CreateEntity(new EntityId(1))));
        toServer.Inject(Frame(catalog, new CreateEntity(new EntityId(2))));

        await Task.WhenAll(
            Task.Run(() => Changes(toServer, catalog, new EntityId(1), changes)),
            Task.Run(() => Changes(toServer, catalog, new EntityId(2), changes)));
        await client.WireClient.Idle;

        Assert.That(client.Reader.Get<CardZone>(client.Reader.ById(new EntityId(1))).Value, Is.EqualTo(Zone.Board));
        Assert.That(client.Reader.Get<CardZone>(client.Reader.ById(new EntityId(2))).Value, Is.EqualTo(Zone.Board));
        Assert.That(client.Services.GetRequiredService<WireLog>().Entries, Has.Count.EqualTo(2 + (2 * changes)));
    }

    // Moves the card between Hand and Board. The last change puts it on the Board.
    private static void Changes(RecordingTransport transport, WireCatalog catalog, EntityId id, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Zone zone = (count - i) % 2 == 1 ? Zone.Board : Zone.Hand;
            transport.Inject(Frame(catalog, new ChangeComponent<CardZone>(id, new CardZone { Value = zone })));
        }
    }

    private static WireReply Reply<T>(T value)
    {
        return new WireReply(null, null, MemoryPackSerializer.Serialize(value));
    }

    private static byte[] Frame(WireCatalog catalog, IMessage entry)
    {
        ArrayBufferWriter<byte> frame = new ArrayBufferWriter<byte>();
        catalog.Write(entry, frame);
        return frame.WrittenSpan.ToArray();
    }
}
