namespace Host.Infrastructure;

public interface ITransport
{
    event FrameHandler? Received;

    void Send(ReadOnlySpan<byte> frame);
}
