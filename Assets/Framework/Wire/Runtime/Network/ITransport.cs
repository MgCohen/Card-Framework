using Unity.Netcode;

namespace Framework.Wire
{
    /// <summary>
    /// Callback fired when a transport hands bytes up to its consumer.
    /// Receiver reads the discriminator + payload from <paramref name="reader"/>.
    /// </summary>
    /// <remarks>
    /// Declared as a delegate (not <c>Action&lt;FastBufferReader&gt;</c>) so consumers
    /// can take <see cref="FastBufferReader"/> by value through the event chain — the type
    /// is a regular <c>struct</c>, but using a typed delegate keeps the signature explicit.
    /// </remarks>
    public delegate void TransportReceiveCallback(FastBufferReader reader);

    /// <summary>
    /// Contract between <see cref="Dispatcher"/> and the underlying byte transport.
    /// <see cref="LoopbackTransport"/> for in-process tests; an NGO-backed transport
    /// will land for Phase 6b.
    /// </summary>
    public interface ITransport
    {
        /// <summary>Push bytes from <paramref name="writer"/> through the transport.</summary>
        void Send(in FastBufferWriter writer);

        /// <summary>Fired whenever the transport delivers an inbound packet.</summary>
        event TransportReceiveCallback OnReceived;
    }
}
