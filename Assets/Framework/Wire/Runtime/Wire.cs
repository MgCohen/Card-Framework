namespace Framework.Wire
{
    public sealed class Wire
    {
        public Wire(EventStream server, EventStream client, Network network)
        {
            server.OnAppended += network.Send;
            network.OnReceived += client.Add;
        }
    }
}
