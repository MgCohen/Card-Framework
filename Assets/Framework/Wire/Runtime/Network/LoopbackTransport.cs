using System;
using Unity.Collections;
using Unity.Netcode;

namespace Framework.Wire
{
    // Synchronous delivery — no Pump step. Send on one peer invokes OnReceived on the other.
    public sealed class LoopbackTransport : ITransport
    {
        LoopbackTransport _peer;

        public event Action<FastBufferReader> OnReceived;

        public void Send(in FastBufferWriter writer)
        {
            var reader = new FastBufferReader(writer, Allocator.Temp);
            try
            {
                _peer.OnReceived?.Invoke(reader);
            }
            finally
            {
                reader.Dispose();
            }
        }

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
