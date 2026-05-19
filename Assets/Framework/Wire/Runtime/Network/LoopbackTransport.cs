using Unity.Collections;
using Unity.Netcode;

namespace Framework.Wire
{
    /// <summary>
    /// In-process transport for tests. Two instances are paired via <see cref="Pair"/>;
    /// a <see cref="Send"/> on one synchronously invokes <see cref="OnReceived"/> on the
    /// other with a matching <see cref="FastBufferReader"/>.
    /// </summary>
    /// <remarks>
    /// Delivery is synchronous on purpose — there is no <c>Pump</c> step. Production
    /// transports (NGO custom messages) deliver on their own cadence, and the framework
    /// provides no flush primitive. Tests written against this transport behave the same
    /// way callers will see production: <c>Send</c> happens, <c>OnReceived</c> fires.
    /// </remarks>
    public sealed class LoopbackTransport : ITransport
    {
        LoopbackTransport _peer;

        public event TransportReceiveCallback OnReceived;

        public void Send(in FastBufferWriter writer)
        {
            // Copy the writer's bytes into a fresh reader buffer so lifetime is local
            // to this synchronous receive. Reader is disposed after the peer's callback
            // returns; allocator is Temp (per-frame arena, free for this pattern).
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

        /// <summary>
        /// Build a paired transport — one for each end of the loopback. Sending on
        /// either invokes <see cref="OnReceived"/> on the other.
        /// </summary>
        public static (LoopbackTransport a, LoopbackTransport b) Pair()
        {
            var a = new LoopbackTransport();
            var b = new LoopbackTransport();
            a._peer = b;
            b._peer = a;
            return (a, b);
        }
    }
}
