using System;

namespace Framework.Wire
{
    public sealed class LoopbackTransport : ITransport
    {
        LoopbackTransport _peer;

        public event TransportReceive OnReceived;

        public void Send(ReadOnlySpan<byte> bytes) => _peer.OnReceived?.Invoke(bytes);

        public static void Pair(out LoopbackTransport serverSide, out LoopbackTransport clientSide)
        {
            serverSide = new LoopbackTransport();
            clientSide = new LoopbackTransport();
            serverSide._peer = clientSide;
            clientSide._peer = serverSide;
        }

        public static LoopbackTransport Solo()
        {
            var t = new LoopbackTransport();
            t._peer = t;
            return t;
        }
    }
}
