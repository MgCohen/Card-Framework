using Network.Domain;
using VSA.Contracts;
using VSA.Contracts.Commands;
using Wire.Contracts.Commands;
using Wire.Handlers.Commands;

namespace Peers.Handlers.Commands;

public sealed class WireClient : IDisposable
{
    private readonly WireCatalog catalog;
    private readonly ICommandSender bus;
    private readonly WireLog log;
    private readonly ITransport server;
    private readonly object gate = new object();
    private readonly ExecutionContext? clean;
    private Task idle = Task.CompletedTask;
    private bool connected;

    public WireClient(WireCatalog catalog, ICommandSender bus, WireLog log, ITransport server)
    {
        this.catalog = catalog;
        this.bus = bus;
        this.log = log;
        this.server = server;
        clean = ExecutionContext.Capture();
        server.OnReceived += Receive;
    }

    public Task Idle
    {
        get
        {
            lock (gate)
            {
                return idle;
            }
        }
    }

    public Task<Result<TReply>> SendAsync<TReply>(IClientMessage<TReply> message, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        server.OnReceived -= Receive;
    }

    private void Receive(ReadOnlySpan<byte> borrowedBytes)
    {
        byte[] frame = borrowedBytes.ToArray();
        lock (gate)
        {
            if (clean == null)
            {
                idle = ApplyAfterAsync(idle, frame);
                return;
            }

            ExecutionContext.Run(clean.CreateCopy(), _ => idle = ApplyAfterAsync(idle, frame), null);
        }
    }

    private async Task ApplyAfterAsync(Task previous, byte[] frame)
    {
        await previous;
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

        Error? error = await catalog.ApplyAsync(entry, bus, log, CancellationToken.None);
        if (error != null)
        {
            throw new InvalidOperationException($"The client did not apply {entry}: {error}.");
        }
    }
}
