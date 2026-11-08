namespace Network.Domain;

public interface ITransport
{
    event TransportReceive? OnReceived;

    void Send(ReadOnlySpan<byte> bytes);
}
