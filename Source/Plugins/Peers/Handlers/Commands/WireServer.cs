using System.Buffers;
using Network.Domain;
using Peers.Contracts.Commands;
using VSA.Contracts;
using VSA.Contracts.Commands;
using Wire.Contracts.Commands;
using Wire.Handlers.Commands;

namespace Peers.Handlers.Commands;

// The server role of a match. It sends the hello and each log entry to each connected client, and runs the client
// messages that it receives on the match bus, one at a time. The reply goes to the sender only.
public sealed class WireServer : IDisposable
{
    private readonly WireLog log;
    private readonly WireCatalog catalog;
    private readonly ICommandSender bus;
    private readonly MatchQueue queue = new MatchQueue();
    private readonly object gate = new object();
    private readonly List<Link> links = new List<Link>();

    public WireServer(WireLog log, WireCatalog catalog, ICommandSender bus, IEnumerable<ITransport> clients)
    {
        this.log = log;
        this.catalog = catalog;
        this.bus = bus;
        foreach (ITransport client in clients)
        {
            Connect(client);
        }

        log.Appended += Broadcast;
    }

    public int Count
    {
        get
        {
            lock (gate)
            {
                return links.Count;
            }
        }
    }

    public void Disconnect(ITransport client)
    {
        Link? link;
        lock (gate)
        {
            link = links.Find(l => ReferenceEquals(l.Transport, client));
            if (link == null)
            {
                return;
            }

            links.Remove(link);
        }

        client.OnReceived -= link.Receive;
    }

    public void Dispose()
    {
        log.Appended -= Broadcast;
        foreach (Link link in Links())
        {
            Disconnect(link.Transport);
        }
    }

    private void Connect(ITransport client)
    {
        Link link = new Link(this, client);
        lock (gate)
        {
            if (links.Exists(l => ReferenceEquals(l.Transport, client)))
            {
                return;
            }

            links.Add(link);
        }

        client.Send(Frame(new WireHello(catalog.Hash)));
        client.OnReceived += link.Receive;
    }

    private bool IsConnected(Link link)
    {
        lock (gate)
        {
            return links.Contains(link);
        }
    }

    private Link[] Links()
    {
        lock (gate)
        {
            return links.ToArray();
        }
    }

    private void Broadcast(IMessage entry)
    {
        byte[] frame = Frame(entry);
        foreach (Link link in Links())
        {
            Send(link, frame);
        }
    }

    // A transport that fails to send loses its client. The other clients still get the frame.
    private void Send(Link link, byte[] frame)
    {
        try
        {
            link.Transport.Send(frame);
        }
        catch (Exception)
        {
            Disconnect(link.Transport);
        }
    }

    private async Task RunAsync(Link link, byte[] frame)
    {
        if (!IsConnected(link))
        {
            return;
        }

        IMessage message;
        try
        {
            message = catalog.Read(frame);
        }
        catch (Exception)
        {
            Disconnect(link.Transport);
            return;
        }

        if (!catalog.IsClientMessage(message.GetType()))
        {
            Disconnect(link.Transport);
            return;
        }

        WireReply reply;
        try
        {
            reply = await catalog.RunAsync(message, bus, CancellationToken.None);
        }
        catch (Exception)
        {
            reply = new WireReply(PeerErrors.HandlerFailed.Code, PeerErrors.HandlerFailed.Message, Array.Empty<byte>());
        }

        if (IsConnected(link))
        {
            Send(link, Frame(reply));
        }
    }

    private byte[] Frame(IMessage entry)
    {
        ArrayBufferWriter<byte> frame = new ArrayBufferWriter<byte>();
        catalog.Write(entry, frame);
        return frame.WrittenSpan.ToArray();
    }

    // One connected client: its transport and the handler that puts its frames in the queue of the match.
    private sealed class Link
    {
        private readonly WireServer server;

        public Link(WireServer server, ITransport transport)
        {
            this.server = server;
            Transport = transport;
        }

        public ITransport Transport { get; }

        public void Receive(ReadOnlySpan<byte> borrowedBytes)
        {
            byte[] frame = borrowedBytes.ToArray();
            server.queue.Run(() => server.RunAsync(this, frame));
        }
    }
}
