using System;
using Unity.Netcode;

namespace Framework.Wire
{
    public interface ITransport
    {
        void Send(in FastBufferWriter writer);
        event Action<FastBufferReader> OnReceived;
    }
}
