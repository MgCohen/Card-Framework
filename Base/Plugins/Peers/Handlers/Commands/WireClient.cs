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
    private readonly object sendGate = new object();
    private readonly object gate = new object();
    private readonly LinkedList<OpenSend> open = new LinkedList<OpenSend>();
    private Exception? stopped;
    private bool connected;

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

        // One lock over the enqueue and the send, so the order of the open sends is the order of the frames.
        lock (sendGate)
        {
            LinkedListNode<OpenSend> node;
            lock (gate)
            {
                if (stopped != null)
                {
                    throw stopped is ObjectDisposedException
                        ? new ObjectDisposedException(nameof(WireClient))
                        : new InvalidOperationException("The client stopped after a frame failed.", stopped);
                }

                node = open.AddLast(send);
            }

            try
            {
                server.Send(frame.WrittenSpan);
            }
            catch (Exception error)
            {
                lock (gate)
                {
                    if (node.List != null)
                    {
                        open.Remove(node);
                    }
                }

                send.Fail(error);
                throw;
            }
        }

        return send.Task;
    }

    public void Dispose()
    {
        server.OnReceived -= Receive;
        Stop(new ObjectDisposedException(nameof(WireClient)));
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
            Stop(error);
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
            if (open.First == null)
            {
                throw new InvalidOperationException("The server sent a reply, and the client has no open send.");
            }

            OpenSend next = open.First.Value;
            open.RemoveFirst();
            return next;
        }
    }

    // A failed frame or a Dispose stops the client. Each open send fails, because no later reply comes, and each
    // later send throws.
    private void Stop(Exception error)
    {
        OpenSend[] failed;
        lock (gate)
        {
            stopped ??= error;
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

        private readonly CancellationTokenRegistration cancel;

        public OpenSend(CancellationToken ct)
        {
            cancel = ct.Register(() => done.TrySetCanceled(ct));
        }

        public Task<Result<TReply>> Task => done.Task;

        public override void Complete(WireReply reply)
        {
            try
            {
                done.TrySetResult(WireCatalog.ReplyOf<TReply>(reply));
            }
            catch (Exception error)
            {
                done.TrySetException(error);
            }

            cancel.Dispose();
        }

        public override void Fail(Exception error)
        {
            done.TrySetException(error);
            cancel.Dispose();
        }
    }
}
