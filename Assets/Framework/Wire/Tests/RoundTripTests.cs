using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.AutoPacker;

namespace Framework.Wire.Tests
{
    public class RoundTripTests
    {
        static (EventStream server, EventStream client, Network network) NewWired(IPackingHandler handler = null)
        {
            var server = new EventStream();
            var client = new EventStream();
            var network = new Network(handler);
            new Wire(server, client, network);
            network.Register<EntityCreatedEvent>();
            network.Register<EntityDestroyedEvent>();
            network.Register<EntityMovedEvent>();
            network.Register<EntityLabeledEvent>();
            network.Register<BlockStartEvent>();
            network.Register<BlockEndEvent>();
            return (server, client, network);
        }

        [Test]
        public void Composite_Payload_RoundTrips()
        {
            var (server, client, network) = NewWired();

            server.Add(new EntityMovedEvent { EntityId = 7, Position = new Vec3(1.5f, 2.5f, 3.5f) });
            network.Pump();

            Assert.AreEqual(1, client.Count);
            var received = (EntityMovedEvent)client[0];
            Assert.AreEqual(7, received.EntityId);
            Assert.AreEqual(1.5f, received.Position.X);
            Assert.AreEqual(2.5f, received.Position.Y);
            Assert.AreEqual(3.5f, received.Position.Z);
        }

        [Test]
        public void Handler_Mapped_Payload_RoundTrips()
        {
            var (server, client, network) = NewWired(new SimplePackingHandler());

            server.Add(new EntityLabeledEvent { EntityId = 11, Label = "alpha" });
            server.Add(new EntityLabeledEvent { EntityId = 12, Label = "beta" });
            server.Add(new EntityLabeledEvent { EntityId = 13, Label = "alpha" });
            network.Pump();

            Assert.AreEqual(3, client.Count);
            Assert.AreEqual("alpha", ((EntityLabeledEvent)client[0]).Label);
            Assert.AreEqual("beta",  ((EntityLabeledEvent)client[1]).Label);
            Assert.AreEqual("alpha", ((EntityLabeledEvent)client[2]).Label);
            Assert.AreEqual(11, ((EntityLabeledEvent)client[0]).EntityId);
            Assert.AreEqual(12, ((EntityLabeledEvent)client[1]).EntityId);
            Assert.AreEqual(13, ((EntityLabeledEvent)client[2]).EntityId);
        }

        [Test]
        public void Event_RoundTrips_From_Server_To_Client()
        {
            var (server, client, network) = NewWired();

            server.Add(new EntityCreatedEvent { EntityId = 42 });
            network.Pump();

            Assert.AreEqual(1, client.Count);
            Assert.AreEqual(42, ((EntityCreatedEvent)client[0]).EntityId);
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
            Assert.AreEqual(1, ((EntityCreatedEvent)client[0]).EntityId);
            Assert.AreEqual(2, ((EntityCreatedEvent)client[1]).EntityId);
            Assert.AreEqual(3, ((EntityCreatedEvent)client[2]).EntityId);
        }

        [Test]
        public void Catalog_Backed_Payload_RoundTrips()
        {
            var catalog = new Catalog();
            var fireball = new CardDefinition(101, "Fireball");
            var healing  = new CardDefinition(102, "Healing Touch");
            catalog.Add(fireball);
            catalog.Add(healing);

            var server = new EventStream();
            var client = new EventStream();
            var network = new Network(new CatalogPackingHandler(catalog));
            new Wire(server, client, network);
            network.Register<CardPlayedEvent>();

            server.Add(new CardPlayedEvent { EntityId = 1, Definition = fireball });
            server.Add(new CardPlayedEvent { EntityId = 2, Definition = healing  });
            network.Pump();

            Assert.AreEqual(2, client.Count);
            Assert.AreSame(fireball, ((CardPlayedEvent)client[0]).Definition);
            Assert.AreSame(healing,  ((CardPlayedEvent)client[1]).Definition);
            Assert.AreEqual(1, ((CardPlayedEvent)client[0]).EntityId);
            Assert.AreEqual(2, ((CardPlayedEvent)client[1]).EntityId);
        }

        [Test]
        public void Two_Handlers_Same_Catalog_Resolve_Identically()
        {
            var catalog = new Catalog();
            var def = new CardDefinition(101, "Fireball");
            catalog.Add(def);

            var a = new CatalogPackingHandler(catalog);
            var b = new CatalogPackingHandler(catalog);

            Assert.AreEqual(a.Resolve<CardDefinition, int>(def), b.Resolve<CardDefinition, int>(def));
            Assert.AreSame(a.Resolve<int, CardDefinition>(101), b.Resolve<int, CardDefinition>(101));
        }

        [Test]
        public void Handlers_With_Different_Catalogs_Are_Isolated()
        {
            var serverCatalog = new Catalog();
            var clientCatalog = new Catalog();
            var serverDef = new CardDefinition(101, "Fireball");
            var clientDef = new CardDefinition(101, "FireballClientCopy");
            serverCatalog.Add(serverDef);
            clientCatalog.Add(clientDef);

            var serverHandler = new CatalogPackingHandler(serverCatalog);
            var clientHandler = new CatalogPackingHandler(clientCatalog);

            Assert.AreSame(serverDef, serverHandler.Resolve<int, CardDefinition>(101));
            Assert.AreSame(clientDef, clientHandler.Resolve<int, CardDefinition>(101));
            Assert.AreNotSame(serverDef, clientDef);
        }

        [Test]
        public void Block_Brackets_RoundTrip()
        {
            var (server, client, network) = NewWired();

            server.Add(new BlockStartEvent { Kind = 1 });
            server.Add(new BlockEndEvent   { Kind = 1 });
            network.Pump();

            Assert.IsInstanceOf<BlockStartEvent>(client[0]);
            Assert.IsInstanceOf<BlockEndEvent>(client[1]);
            Assert.AreEqual(1, ((BlockStartEvent)client[0]).Kind);
            Assert.AreEqual(1, ((BlockEndEvent)client[1]).Kind);
        }

        [Test]
        public void Nested_Blocks_Preserve_Order_And_Kind()
        {
            var (server, client, network) = NewWired();

            server.Add(new BlockStartEvent { Kind = 1 });
            server.Add(new BlockStartEvent { Kind = 2 });
            server.Add(new BlockEndEvent   { Kind = 2 });
            server.Add(new BlockEndEvent   { Kind = 1 });
            network.Pump();

            Assert.AreEqual(4, client.Count);
            Assert.AreEqual(1, ((BlockStartEvent)client[0]).Kind);
            Assert.AreEqual(2, ((BlockStartEvent)client[1]).Kind);
            Assert.AreEqual(2, ((BlockEndEvent)  client[2]).Kind);
            Assert.AreEqual(1, ((BlockEndEvent)  client[3]).Kind);
        }

        [Test]
        public void Mixed_Alphabet_Stream_Preserves_Order()
        {
            var (server, client, network) = NewWired(new SimplePackingHandler());

            server.Add(new BlockStartEvent     { Kind = 1 });
            server.Add(new EntityCreatedEvent  { EntityId = 1 });
            server.Add(new EntityCreatedEvent  { EntityId = 2 });
            server.Add(new EntityMovedEvent    { EntityId = 1, Position = new Vec3(10, 20, 30) });
            server.Add(new EntityLabeledEvent  { EntityId = 1, Label = "hero" });
            server.Add(new EntityDestroyedEvent { EntityId = 1 });
            server.Add(new BlockEndEvent       { Kind = 1 });
            network.Pump();

            Assert.AreEqual(7, client.Count);
            Assert.IsInstanceOf<BlockStartEvent>    (client[0]);
            Assert.IsInstanceOf<EntityCreatedEvent> (client[1]);
            Assert.IsInstanceOf<EntityCreatedEvent> (client[2]);
            Assert.IsInstanceOf<EntityMovedEvent>   (client[3]);
            Assert.IsInstanceOf<EntityLabeledEvent> (client[4]);
            Assert.IsInstanceOf<EntityDestroyedEvent>(client[5]);
            Assert.IsInstanceOf<BlockEndEvent>      (client[6]);

            Assert.AreEqual(1, ((EntityCreatedEvent)client[1]).EntityId);
            Assert.AreEqual(2, ((EntityCreatedEvent)client[2]).EntityId);
            Assert.AreEqual(20, ((EntityMovedEvent)client[3]).Position.Y);
            Assert.AreEqual("hero", ((EntityLabeledEvent)client[4]).Label);
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
