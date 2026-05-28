namespace Framework.Wire
{
    // Server-side bridge: every event appended to the local EventStream is forwarded to
    // Network. Single subscription, no per-type ceremony — Network.Send routes by runtime
    // type via the channel dictionary.
    internal sealed class WireForwarder
    {
        public WireForwarder(EventStream stream, Network network)
            => stream.OnAppended += network.Send;
    }
}
