using Network.Domain;

namespace Testing.Domain;

public sealed class RecordingTransport : ITransport
{
    private readonly List<byte[]> sent = new List<byte[]>();

    public IReadOnlyList<byte[]> Sent => sent;

    public event TransportReceive? OnReceived;

    public void Inject(byte[] frame)
    {
        OnReceived?.Invoke(frame);
    }

    public void Send(ReadOnlySpan<byte> bytes)
    {
        sent.Add(bytes.ToArray());
    }
}
