using System;

namespace Framework.Wire
{
    public sealed class NullTransport : ITransport
    {
        public event TransportReceive OnReceived
        {
            add { }
            remove { }
        }

        public void Send(ReadOnlySpan<byte> bytes) { }
    }
}
