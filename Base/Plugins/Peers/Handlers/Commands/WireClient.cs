using System.Buffers;
using Network.Domain;
using VSA.Contracts;
using VSA.Contracts.Commands;
using Wire.Contracts.Commands;
using Wire.Handlers.Commands;

namespace Peers.Handlers.Commands;

// The client role of a match. It applies the frames of the server in order, on its own bus, and sends client messages
// to the server. The transport keeps the order, so each reply completes the oldest open send.
public sealed class WireClient : IDisposable
{
    private readonly WireCatalog catalog;
    private readonly ICommandSender bus;
    private readonly WireLog log;
    private readonly ITransport server;
    private readonly MatchQueue queue = new MatchQueue();
    private readonly object gate = new object();
    private readonly Queue<OpenSend> open = new Queue<OpenSend>();
    private bool connected;
    private bool disposed;

    public WireClient(WireCatalog catalog, ICommandSender bus, WireLog log, ITransport server)
    {
        this.catalog = catalog;
        this.bus = bus;
        this.log = log;
        this.server = server;
        server.OnReceived += Receive;
    }

    // Completes when each received frame is applied. It faults when a frame fails, and no later frame applies.
    public Task Idle => queue.Idle;

    public Task<Result<TReply>> SendAsync<TReply>(IClientMessage<TReply> message, CancellationToken ct)
    {
        ArrayBufferWriter<byte> frame = new ArrayBufferWriter<byte>();
        catalog.Write(message, frame);
        OpenSend<TReply> send = new OpenSend<TReply>(ct);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            open.Enqueue(send);
        }

        server.Send(frame.WrittenSpan);
        return send.Task;
    }

    public void Dispose()
    {
        server.OnReceived -= Receive;
        Fail(new ObjectDisposedException(nameof(WireClient)), dispose: true);
    }

    private void Receive(ReadOnlySpan<byte> borrowedBytes)
    {
        byte[] frame = borrowedBytes.ToArray();
        queue.Run(() => ApplyAsync(frame));
    }

    private async Task ApplyAsync(byte[] frame)
    {
        try
        {
            await ApplyFrameAsync(frame);
        }
        catch (Exception error)
        {
            Fail(error, dispose: false);
            throw;
        }
    }

    private async Task ApplyFrameAsync(byte[] frame)
    {
        IMessage entry = catalog.Read(frame);
        if (entry is WireHello hello)
        {
            if (hello.Hash != catalog.Hash)
            {
                throw new InvalidOperationException(
                    $"The server catalog hash is {hello.Hash:X16}, and the client catalog hash is {catalog.Hash:X16}. "
                    + "Use the same module assemblies and the same extra WireTypes on the server and the client.");
            }

            connected = true;
            return;
        }

        if (!connected)
        {
            throw new InvalidOperationException($"{entry} came before the hello of the server.");
        }

        if (entry is WireReply reply)
        {
            Next().Complete(reply);
            return;
        }

        Error? error = await catalog.ApplyAsync(entry, bus, log, CancellationToken.None);
        if (error != null)
        {
            throw new InvalidOperationException($"The client did not apply {entry}: {error}.");
        }
    }

    private OpenSend Next()
    {
        lock (gate)
        {
            if (open.Count == 0)
            {
                throw new InvalidOperationException("The server sent a reply, and the client has no open send.");
            }

            return open.Dequeue();
        }
    }

    // A failed frame or a Dispose fails each open send, because no later reply comes.
    private void Fail(Exception error, bool dispose)
    {
        OpenSend[] failed;
        lock (gate)
        {
            disposed |= dispose;
            failed = open.ToArray();
            open.Clear();
        }

        foreach (OpenSend send in failed)
        {
            send.Fail(error);
        }
    }

    private abstract class OpenSend
    {
        public abstract void Complete(WireReply reply);

        public abstract void Fail(Exception error);
    }

    private sealed class OpenSend<TReply> : OpenSend
    {
        private readonly TaskCompletionSource<Result<TReply>> done =
            new TaskCompletionSource<Result<TReply>>(TaskCreationOptions.RunContinuationsAsynchronously);

        public OpenSend(CancellationToken ct)
        {
            if (ct.CanBeCanceled)
            {
                ct.Register(() => done.TrySetCanceled(ct));
            }
        }

        public Task<Result<TReply>> Task => done.Task;

        public override void Complete(WireReply reply)
        {
            done.TrySetResult(WireCatalog.ReplyOf<TReply>(reply));
        }

        public override void Fail(Exception error)
        {
            done.TrySetException(error);
        }
    }
}
