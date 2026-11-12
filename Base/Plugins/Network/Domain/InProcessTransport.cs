namespace Network.Domain;

public sealed class InProcessTransport : ITransport
{
    private InProcessTransport? peer;

    private InProcessTransport()
    {
    }

    public event TransportReceive? OnReceived;

    public static void Pair(out InProcessTransport serverSide, out InProcessTransport clientSide)
    {
        serverSide = new InProcessTransport();
        clientSide = new InProcessTransport();
        serverSide.peer = clientSide;
        clientSide.peer = serverSide;
    }

    public void Send(ReadOnlySpan<byte> bytes)
    {
        peer!.OnReceived?.Invoke(bytes);
    }
}
