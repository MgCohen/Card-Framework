using System.Collections.Generic;
using NUnit.Framework;

namespace Framework.Wire.Tests
{
    public class RoundTripTests
    {
        static (EventStream server, EventStream client, Network network) NewWired()
        {
            var server = new EventStream();
            var client = new EventStream();
            var network = new Network();
            new Wire(server, client, network);
            network.Register<EntityCreatedEvent>();
            network.Register<EntityDestroyedEvent>();
            return (server, client, network);
        }

        [Test]
        public void Event_RoundTrips_From_Server_To_Client()
        {
            var (server, client, network) = NewWired();

            server.Add(new EntityCreatedEvent { EntityId = 42 });
            network.Pump();

            Assert.AreEqual(1, client.Count);
            Assert.AreEqual(42, ((EntityCreatedEvent)client.Get(0)).EntityId);
        }

        [Test]
        public void Client_Stream_Preserves_Send_Order_Within_One_Channel()
        {
            var (server, client, network) = NewWired();

            server.Add(new EntityCreatedEvent { EntityId = 1 });
            server.Add(new EntityCreatedEvent { EntityId = 2 });
            server.Add(new EntityCreatedEvent { EntityId = 3 });
            network.Pump();

            Assert.AreEqual(3, client.Count);
            Assert.AreEqual(1, ((EntityCreatedEvent)client.Get(0)).EntityId);
            Assert.AreEqual(2, ((EntityCreatedEvent)client.Get(1)).EntityId);
            Assert.AreEqual(3, ((EntityCreatedEvent)client.Get(2)).EntityId);
        }

        [Test]
        public void Client_Stream_Preserves_Send_Order_Across_Channels()
        {
            var (server, client, network) = NewWired();

            server.Add(new EntityCreatedEvent   { EntityId = 1 });
            server.Add(new EntityDestroyedEvent { EntityId = 1 });
            server.Add(new EntityCreatedEvent   { EntityId = 2 });
            server.Add(new EntityDestroyedEvent { EntityId = 2 });
            network.Pump();

            Assert.AreEqual(4, client.Count);
            Assert.IsInstanceOf<EntityCreatedEvent>  (client.Get(0));
            Assert.IsInstanceOf<EntityDestroyedEvent>(client.Get(1));
            Assert.IsInstanceOf<EntityCreatedEvent>  (client.Get(2));
            Assert.IsInstanceOf<EntityDestroyedEvent>(client.Get(3));
            Assert.AreEqual(1, ((EntityCreatedEvent)  client.Get(0)).EntityId);
            Assert.AreEqual(1, ((EntityDestroyedEvent)client.Get(1)).EntityId);
            Assert.AreEqual(2, ((EntityCreatedEvent)  client.Get(2)).EntityId);
            Assert.AreEqual(2, ((EntityDestroyedEvent)client.Get(3)).EntityId);
        }

        [Test]
        public void Typed_Channel_Subscription_Receives_Typed_Event()
        {
            var server = new EventStream();
            var client = new EventStream();
            var network = new Network();
            new Wire(server, client, network);
            var channel = network.Register<EntityCreatedEvent>();

            EntityCreatedEvent typed = null;
            channel.OnReceived += ev => typed = ev;

            server.Add(new EntityCreatedEvent { EntityId = 99 });
            network.Pump();

            Assert.IsNotNull(typed);
            Assert.AreEqual(99, typed.EntityId);
        }

        [Test]
        public void Client_Stream_OnAppended_Fires_After_Pump_Not_Send()
        {
            var (server, client, network) = NewWired();
            var seen = new List<long>();
            client.OnAppended += ev => seen.Add(((EntityCreatedEvent)ev).EntityId);

            server.Add(new EntityCreatedEvent { EntityId = 10 });
            server.Add(new EntityCreatedEvent { EntityId = 20 });

            Assert.AreEqual(0, seen.Count, "Client stream must not see events before network.Pump");
            network.Pump();
            CollectionAssert.AreEqual(new long[] { 10, 20 }, seen);
        }
    }
}
