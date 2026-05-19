using NUnit.Framework;

namespace Framework.Wire.Tests
{
    public class TwoEndTests
    {
        [Test]
        public void Explicit_Two_Network_Setup_RoundTrips()
        {
            var (serverTransport, clientTransport) = LoopbackTransport.Pair();
            var serverNetwork = new Network(serverTransport);
            var clientNetwork = new Network(clientTransport);

            serverNetwork.Register<EntityCreatedEvent>();
            var clientChannel = clientNetwork.Register<EntityCreatedEvent>();

            EntityCreatedEvent received = null;
            clientChannel.OnReceived += ev => received = ev;

            serverNetwork.Send(new EntityCreatedEvent { EntityId = 42 });

            Assert.IsNotNull(received);
            Assert.AreEqual(42, received.EntityId);
        }

        [Test]
        public void Direct_Typed_Send_On_Channel_Skips_Type_Lookup()
        {
            var (serverTransport, clientTransport) = LoopbackTransport.Pair();
            var serverNetwork = new Network(serverTransport);
            var clientNetwork = new Network(clientTransport);

            var serverChannel = serverNetwork.Register<EntityCreatedEvent>();
            var clientChannel = clientNetwork.Register<EntityCreatedEvent>();

            EntityCreatedEvent received = null;
            clientChannel.OnReceived += ev => received = ev;

            serverChannel.Send(new EntityCreatedEvent { EntityId = 99 });

            Assert.IsNotNull(received);
            Assert.AreEqual(99, received.EntityId);
        }

        [Test]
        public void Channel_Ids_Match_When_Registration_Order_Matches()
        {
            var (serverTransport, clientTransport) = LoopbackTransport.Pair();
            var serverNetwork = new Network(serverTransport);
            var clientNetwork = new Network(clientTransport);

            serverNetwork.Register<EntityCreatedEvent>();
            serverNetwork.Register<EntityDestroyedEvent>();
            var clientDestroyChannel = clientNetwork.Register<EntityCreatedEvent>();
            var clientCreateOrder = clientNetwork.Register<EntityDestroyedEvent>();

            int createdSeen = 0, destroyedSeen = 0;
            clientDestroyChannel.OnReceived += _ => createdSeen++;
            clientCreateOrder.OnReceived += _ => destroyedSeen++;

            serverNetwork.Send(new EntityCreatedEvent { EntityId = 1 });
            serverNetwork.Send(new EntityDestroyedEvent { EntityId = 1 });

            Assert.AreEqual(1, createdSeen);
            Assert.AreEqual(1, destroyedSeen);
        }

        [Test]
        public void Network_OnReceived_Fires_For_Every_Inbound_Event()
        {
            var (serverTransport, clientTransport) = LoopbackTransport.Pair();
            var serverNetwork = new Network(serverTransport);
            var clientNetwork = new Network(clientTransport);

            serverNetwork.Register<EntityCreatedEvent>();
            clientNetwork.Register<EntityCreatedEvent>();

            int count = 0;
            clientNetwork.OnReceived += _ => count++;

            serverNetwork.Send(new EntityCreatedEvent { EntityId = 1 });
            serverNetwork.Send(new EntityCreatedEvent { EntityId = 2 });
            serverNetwork.Send(new EntityCreatedEvent { EntityId = 3 });

            Assert.AreEqual(3, count);
        }
    }
}
