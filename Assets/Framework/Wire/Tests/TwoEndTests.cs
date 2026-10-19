using NUnit.Framework;

namespace Framework.Wire.Tests
{
    public class TwoEndTests
    {
        [Test]
        public void Explicit_Two_Network_Setup_RoundTrips()
        {
            LoopbackTransport.Pair(out var serverTransport, out var clientTransport);
            var serverNetwork = new Network(serverTransport);
            var clientNetwork = new Network(clientTransport);

            serverNetwork.Register<PingEvent, PingEvent.Packed>();
            var clientChannel = clientNetwork.Register<PingEvent, PingEvent.Packed>();

            PingEvent received = null;
            clientChannel.OnReceived += ev => received = ev;

            serverNetwork.Send(new PingEvent { Value = 42 });

            Assert.IsNotNull(received);
            Assert.AreEqual(42, received.Value);
        }

        [Test]
        public void Direct_Typed_Send_On_Channel_Skips_Type_Lookup()
        {
            LoopbackTransport.Pair(out var serverTransport, out var clientTransport);
            var serverNetwork = new Network(serverTransport);
            var clientNetwork = new Network(clientTransport);

            var serverChannel = serverNetwork.Register<PingEvent, PingEvent.Packed>();
            var clientChannel = clientNetwork.Register<PingEvent, PingEvent.Packed>();

            PingEvent received = null;
            clientChannel.OnReceived += ev => received = ev;

            serverChannel.Send(new PingEvent { Value = 99 });

            Assert.IsNotNull(received);
            Assert.AreEqual(99, received.Value);
        }

        [Test]
        public void Channel_Ids_Match_When_Registration_Order_Matches()
        {
            LoopbackTransport.Pair(out var serverTransport, out var clientTransport);
            var serverNetwork = new Network(serverTransport);
            var clientNetwork = new Network(clientTransport);

            serverNetwork.Register<PingEvent, PingEvent.Packed>();
            serverNetwork.Register<PongEvent, PongEvent.Packed>();
            var clientPingChannel = clientNetwork.Register<PingEvent, PingEvent.Packed>();
            var clientPongChannel = clientNetwork.Register<PongEvent, PongEvent.Packed>();

            int pingSeen = 0, pongSeen = 0;
            clientPingChannel.OnReceived += _ => pingSeen++;
            clientPongChannel.OnReceived += _ => pongSeen++;

            serverNetwork.Send(new PingEvent { Value = 1 });
            serverNetwork.Send(new PongEvent { Value = 1 });

            Assert.AreEqual(1, pingSeen);
            Assert.AreEqual(1, pongSeen);
        }

        [Test]
        public void Network_OnReceived_Fires_For_Every_Inbound_Event()
        {
            LoopbackTransport.Pair(out var serverTransport, out var clientTransport);
            var serverNetwork = new Network(serverTransport);
            var clientNetwork = new Network(clientTransport);

            serverNetwork.Register<PingEvent, PingEvent.Packed>();
            clientNetwork.Register<PingEvent, PingEvent.Packed>();

            int count = 0;
            clientNetwork.OnReceived += _ => count++;

            serverNetwork.Send(new PingEvent { Value = 1 });
            serverNetwork.Send(new PingEvent { Value = 2 });
            serverNetwork.Send(new PingEvent { Value = 3 });

            Assert.AreEqual(3, count);
        }

        [Test]
        public void Client_Send_Reaches_Server_Network()
        {
            LoopbackTransport.Pair(out var serverTransport, out var clientTransport);
            var serverNetwork = new Network(serverTransport);
            var clientNetwork = new Network(clientTransport);

            var serverChannel = serverNetwork.Register<PingEvent, PingEvent.Packed>();
            clientNetwork.Register<PingEvent, PingEvent.Packed>();

            PingEvent received = null;
            serverChannel.OnReceived += ev => received = ev;

            clientNetwork.Send(new PingEvent { Value = 7 });

            Assert.IsNotNull(received);
            Assert.AreEqual(7, received.Value);
        }

        // Characterizes today's wiring: no server-side inbound bridge exists, so a client
        // send reaches the server Network but never enters the server EventStream.
        // Flip this when the client -> server path is wired.
        [Test]
        public void Client_Send_Does_Not_Enter_Server_Stream()
        {
            var w = Wire.Connect();
            w.ServerNetwork.Register<PingEvent, PingEvent.Packed>();
            w.ClientNetwork.Register<PingEvent, PingEvent.Packed>();

            w.ClientNetwork.Send(new PingEvent { Value = 7 });

            Assert.AreEqual(0, w.ServerStream.Count);
        }
    }
}
