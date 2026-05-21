using System;
using Unity.Netcode;

namespace Framework.Wire
{
    public sealed class NullTransport : ITransport
    {
        public event Action<FastBufferReader> OnReceived
        {
            add { _ = value; }
            remove { _ = value; }
        }

        public void Send(in FastBufferWriter writer) { }
    }
}
