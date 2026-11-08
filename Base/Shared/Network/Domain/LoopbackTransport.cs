namespace Network.Domain;

public sealed class LoopbackTransport : ITransport
{
    private LoopbackTransport? peer;

    private LoopbackTransport()
    {
    }

    public event TransportReceive? OnReceived;

    public static void Pair(out LoopbackTransport serverSide, out LoopbackTransport clientSide)
    {
        serverSide = new LoopbackTransport();
        clientSide = new LoopbackTransport();
        serverSide.peer = clientSide;
        clientSide.peer = serverSide;
    }

    public void Send(ReadOnlySpan<byte> bytes)
    {
        peer!.OnReceived?.Invoke(bytes);
    }
}
