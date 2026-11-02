using System;
using System.Collections.Generic;

namespace Framework.Wire.Tests
{
    // Records each sent frame and injects frames as if they came from a peer.
    public sealed class CaptureTransport : ITransport
    {
        public readonly List<byte[]> Sent = new();

        public event TransportReceive OnReceived;

        public void Send(ReadOnlySpan<byte> bytes) => Sent.Add(bytes.ToArray());

        public void Inject(byte[] frame) => OnReceived?.Invoke(frame);
    }
}
