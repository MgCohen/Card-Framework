using Network.Domain;

namespace Testing;

public sealed class RecordingTransport : ITransport
{
    private readonly List<byte[]> sent;

    public RecordingTransport()
    {
        sent = new List<byte[]>();
        Sent = sent.AsReadOnly();
    }

    public IReadOnlyList<byte[]> Sent { get; }

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
