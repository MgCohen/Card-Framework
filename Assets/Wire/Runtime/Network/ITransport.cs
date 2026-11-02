using System;

namespace Framework.Wire
{
    // The span is valid only during the call. Copy the bytes to keep them.
    public delegate void TransportReceive(ReadOnlySpan<byte> bytes);

    public interface ITransport
    {
        void Send(ReadOnlySpan<byte> bytes);
        event TransportReceive OnReceived;
    }
}
