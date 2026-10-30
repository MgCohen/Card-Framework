namespace Framework.Wire
{
    internal sealed class WireForwarder
    {
        public WireForwarder(EventStream stream, Network network)
            => stream.OnAppended += network.Send;
    }
}
