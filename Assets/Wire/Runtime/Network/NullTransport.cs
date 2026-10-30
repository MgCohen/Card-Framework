using System;
using Unity.Netcode;

namespace Framework.Wire
{
    public sealed class NullTransport : ITransport
    {
        public event Action<FastBufferReader> OnReceived
        {
            add { }
            remove { }
        }

        public void Send(in FastBufferWriter writer) { }
    }
}
