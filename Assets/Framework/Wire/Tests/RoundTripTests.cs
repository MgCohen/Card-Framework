using System.Collections.Generic;
using NUnit.Framework;
using Scaffold.AutoPacker;

namespace Framework.Wire.Tests
{
    public class RoundTripTests
    {
        static WireConnection NewWired(IPackingHandler handler = null)
        {
            var w = Wire.Connect(handler);
            w.ServerNetwork.Register<EntityCreatedEvent, EntityCreatedEvent.Packed>();   w.ClientNetwork.Register<EntityCreatedEvent, EntityCreatedEvent.Packed>();
            w.ServerNetwork.Register<EntityDestroyedEvent, EntityDestroyedEvent.Packed>(); w.ClientNetwork.Register<EntityDestroyedEvent, EntityDestroyedEvent.Packed>();
            w.ServerNetwork.Register<EntityMovedEvent, EntityMovedEvent.Packed>();     w.ClientNetwork.Register<EntityMovedEvent, EntityMovedEvent.Packed>();
            w.ServerNetwork.Register<EntityLabeledEvent, EntityLabeledEvent.Packed>();   w.ClientNetwork.Register<EntityLabeledEvent, EntityLabeledEvent.Packed>();
            w.ServerNetwork.Register<BlockStartEvent, BlockStartEvent.Packed>();      w.ClientNetwork.Register<BlockStartEvent, BlockStartEvent.Packed>();
            w.ServerNetwork.Register<BlockEndEvent, BlockEndEvent.Packed>();        w.ClientNetwork.Register<BlockEndEvent, BlockEndEvent.Packed>();
            w.ServerNetwork.Register<RequestOpenedEvent, RequestOpenedEvent.Packed>();   w.ClientNetwork.Register<RequestOpenedEvent, RequestOpenedEvent.Packed>();
            w.ServerNetwork.Register<RequestClosedEvent, RequestClosedEvent.Packed>();   w.ClientNetwork.Register<RequestClosedEvent, RequestClosedEvent.Packed>();
            w.ServerNetwork.Register<ResponseEvent, ResponseEvent.Packed>();        w.ClientNetwork.Register<ResponseEvent, ResponseEvent.Packed>();
            return w;
        }

        [Test]
        public void Composite_Payload_RoundTrips()
        {
            var w = NewWired();

            w.ServerStream.Emit(new EntityMovedEvent { EntityId = 7, Position = new Vec3(1.5f, 2.5f, 3.5f) });

            Assert.AreEqual(1, w.ClientStream.Count);
            var received = (EntityMovedEvent)w.ClientStream[0];
            Assert.AreEqual(7, received.EntityId);
            Assert.AreEqual(1.5f, received.Position.X);
            Assert.AreEqual(2.5f, received.Position.Y);
            Assert.AreEqual(3.5f, received.Position.Z);
        }

        [Test]
        public void Handler_Mapped_Payload_RoundTrips()
        {
            var w = NewWired(new SimplePackingHandler());

            w.ServerStream.Emit(new EntityLabeledEvent { EntityId = 11, Label = "alpha" });
            w.ServerStream.Emit(new EntityLabeledEvent { EntityId = 12, Label = "beta" });
            w.ServerStream.Emit(new EntityLabeledEvent { EntityId = 13, Label = "alpha" });

            Assert.AreEqual(3, w.ClientStream.Count);
            Assert.AreEqual("alpha", ((EntityLabeledEvent)w.ClientStream[0]).Label);
            Assert.AreEqual("beta",  ((EntityLabeledEvent)w.ClientStream[1]).Label);
            Assert.AreEqual("alpha", ((EntityLabeledEvent)w.ClientStream[2]).Label);
            Assert.AreEqual(11, ((EntityLabeledEvent)w.ClientStream[0]).EntityId);
            Assert.AreEqual(12, ((EntityLabeledEvent)w.ClientStream[1]).EntityId);
            Assert.AreEqual(13, ((EntityLabeledEvent)w.ClientStream[2]).EntityId);
        }

        [Test]
        public void Event_RoundTrips_From_Server_To_Client()
        {
            var w = NewWired();

            w.ServerStream.Emit(new EntityCreatedEvent { EntityId = 42 });

            Assert.AreEqual(1, w.ClientStream.Count);
            Assert.AreEqual(42, ((EntityCreatedEvent)w.ClientStream[0]).EntityId);
        }

        [Test]
        public void Client_Stream_Preserves_Send_Order_Within_One_Channel()
        {
            var w = NewWired();

            w.ServerStream.Emit(new EntityCreatedEvent { EntityId = 1 });
            w.ServerStream.Emit(new EntityCreatedEvent { EntityId = 2 });
            w.ServerStream.Emit(new EntityCreatedEvent { EntityId = 3 });

            Assert.AreEqual(3, w.ClientStream.Count);
            Assert.AreEqual(1, ((EntityCreatedEvent)w.ClientStream[0]).EntityId);
            Assert.AreEqual(2, ((EntityCreatedEvent)w.ClientStream[1]).EntityId);
            Assert.AreEqual(3, ((EntityCreatedEvent)w.ClientStream[2]).EntityId);
        }

        [Test]
        public void Catalog_Backed_Payload_RoundTrips()
        {
            var catalog = new Catalog();
            var fireball = new CardDefinition(101, "Fireball");
            var healing  = new CardDefinition(102, "Healing Touch");
            catalog.Add(fireball);
            catalog.Add(healing);

            var w = Wire.Connect(new CatalogPackingHandler(catalog));
            w.ServerNetwork.Register<CardPlayedEvent, CardPlayedEvent.Packed>();
            w.ClientNetwork.Register<CardPlayedEvent, CardPlayedEvent.Packed>();

            w.ServerStream.Emit(new CardPlayedEvent { EntityId = 1, Definition = fireball });
            w.ServerStream.Emit(new CardPlayedEvent { EntityId = 2, Definition = healing  });

            Assert.AreEqual(2, w.ClientStream.Count);
            Assert.AreSame(fireball, ((CardPlayedEvent)w.ClientStream[0]).Definition);
            Assert.AreSame(healing,  ((CardPlayedEvent)w.ClientStream[1]).Definition);
            Assert.AreEqual(1, ((CardPlayedEvent)w.ClientStream[0]).EntityId);
            Assert.AreEqual(2, ((CardPlayedEvent)w.ClientStream[1]).EntityId);
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
            var w = NewWired();

            w.ServerStream.Emit(new BlockStartEvent { Id = 1, ParentId = 0, Kind = (byte)BlockKind.Command, SourceId = 7 });
            w.ServerStream.Emit(new BlockEndEvent   { Id = 1, Outcome = (byte)BlockOutcome.Completed });

            Assert.IsInstanceOf<BlockStartEvent>(w.ClientStream[0]);
            Assert.IsInstanceOf<BlockEndEvent>(w.ClientStream[1]);

            var start = (BlockStartEvent)w.ClientStream[0];
            Assert.AreEqual(1, start.Id);
            Assert.AreEqual(0, start.ParentId);
            Assert.AreEqual((byte)BlockKind.Command, start.Kind);
            Assert.AreEqual(7, start.SourceId);

            var end = (BlockEndEvent)w.ClientStream[1];
            Assert.AreEqual(1, end.Id);
            Assert.AreEqual((byte)BlockOutcome.Completed, end.Outcome);
        }

        [Test]
        public void Nested_Blocks_Preserve_Order_And_ParentId()
        {
            var w = NewWired();

            w.ServerStream.Emit(new BlockStartEvent { Id = 1, ParentId = 0, Kind = (byte)BlockKind.Command, SourceId = 7 });
            w.ServerStream.Emit(new BlockStartEvent { Id = 2, ParentId = 1, Kind = (byte)BlockKind.Effect,  SourceId = 7 });
            w.ServerStream.Emit(new BlockEndEvent   { Id = 2, Outcome = (byte)BlockOutcome.Completed });
            w.ServerStream.Emit(new BlockEndEvent   { Id = 1, Outcome = (byte)BlockOutcome.Completed });

            Assert.AreEqual(4, w.ClientStream.Count);

            var outerStart = (BlockStartEvent)w.ClientStream[0];
            var innerStart = (BlockStartEvent)w.ClientStream[1];
            Assert.AreEqual(1, outerStart.Id);
            Assert.AreEqual(0, outerStart.ParentId);
            Assert.AreEqual(2, innerStart.Id);
            Assert.AreEqual(1, innerStart.ParentId);
            Assert.AreEqual((byte)BlockKind.Command, outerStart.Kind);
            Assert.AreEqual((byte)BlockKind.Effect,  innerStart.Kind);

            Assert.AreEqual(2, ((BlockEndEvent)w.ClientStream[2]).Id);
            Assert.AreEqual(1, ((BlockEndEvent)w.ClientStream[3]).Id);
        }

        [Test]
        public void BlockEnd_Outcomes_RoundTrip()
        {
            var w = NewWired();

            w.ServerStream.Emit(new BlockEndEvent { Id = 1, Outcome = (byte)BlockOutcome.Completed });
            w.ServerStream.Emit(new BlockEndEvent { Id = 2, Outcome = (byte)BlockOutcome.Errored   });
            w.ServerStream.Emit(new BlockEndEvent { Id = 3, Outcome = (byte)BlockOutcome.Cancelled });

            Assert.AreEqual((byte)BlockOutcome.Completed, ((BlockEndEvent)w.ClientStream[0]).Outcome);
            Assert.AreEqual((byte)BlockOutcome.Errored,   ((BlockEndEvent)w.ClientStream[1]).Outcome);
            Assert.AreEqual((byte)BlockOutcome.Cancelled, ((BlockEndEvent)w.ClientStream[2]).Outcome);
        }

        [Test]
        public void Request_Open_Close_Response_RoundTrip()
        {
            var w = NewWired();

            w.ServerStream.Emit(new RequestOpenedEvent { Id = 3, ParentId = 2, SourceId = 7, ResponderId = 42, OptionCount = 3, IsCancellable = true });
            w.ServerStream.Emit(new RequestClosedEvent { Id = 3 });
            w.ServerStream.Emit(new ResponseEvent      { RequestId = 3, Picked = 1 });

            Assert.AreEqual(3, w.ClientStream.Count);

            var opened = (RequestOpenedEvent)w.ClientStream[0];
            Assert.AreEqual(3,    opened.Id);
            Assert.AreEqual(2,    opened.ParentId);
            Assert.AreEqual(7,    opened.SourceId);
            Assert.AreEqual(42,   opened.ResponderId);
            Assert.AreEqual(3,    opened.OptionCount);
            Assert.IsTrue(opened.IsCancellable);

            var closed = (RequestClosedEvent)w.ClientStream[1];
            Assert.AreEqual(3, closed.Id);

            var response = (ResponseEvent)w.ClientStream[2];
            Assert.AreEqual(3, response.RequestId);
            Assert.AreEqual(1, response.Picked);
        }

        [Test]
        public void Request_NonCancellable_RoundTrips()
        {
            var w = NewWired();

            w.ServerStream.Emit(new RequestOpenedEvent { Id = 5, ParentId = 0, SourceId = 0, ResponderId = 99, OptionCount = 1, IsCancellable = false });

            var opened = (RequestOpenedEvent)w.ClientStream[0];
            Assert.IsFalse(opened.IsCancellable);
            Assert.AreEqual(1, opened.OptionCount);
        }

        [Test]
        public void Mixed_Alphabet_Stream_Preserves_Order()
        {
            var w = NewWired(new SimplePackingHandler());

            w.ServerStream.Emit(new BlockStartEvent     { Id = 1, ParentId = 0, Kind = (byte)BlockKind.Command, SourceId = 0 });
            w.ServerStream.Emit(new EntityCreatedEvent  { EntityId = 1 });
            w.ServerStream.Emit(new EntityCreatedEvent  { EntityId = 2 });
            w.ServerStream.Emit(new EntityMovedEvent    { EntityId = 1, Position = new Vec3(10, 20, 30) });
            w.ServerStream.Emit(new EntityLabeledEvent  { EntityId = 1, Label = "hero" });
            w.ServerStream.Emit(new EntityDestroyedEvent { EntityId = 1 });
            w.ServerStream.Emit(new BlockEndEvent       { Id = 1, Outcome = (byte)BlockOutcome.Completed });

            Assert.AreEqual(7, w.ClientStream.Count);
            Assert.IsInstanceOf<BlockStartEvent>    (w.ClientStream[0]);
            Assert.IsInstanceOf<EntityCreatedEvent> (w.ClientStream[1]);
            Assert.IsInstanceOf<EntityCreatedEvent> (w.ClientStream[2]);
            Assert.IsInstanceOf<EntityMovedEvent>   (w.ClientStream[3]);
            Assert.IsInstanceOf<EntityLabeledEvent> (w.ClientStream[4]);
            Assert.IsInstanceOf<EntityDestroyedEvent>(w.ClientStream[5]);
            Assert.IsInstanceOf<BlockEndEvent>      (w.ClientStream[6]);

            Assert.AreEqual(1, ((EntityCreatedEvent)w.ClientStream[1]).EntityId);
            Assert.AreEqual(2, ((EntityCreatedEvent)w.ClientStream[2]).EntityId);
            Assert.AreEqual(20, ((EntityMovedEvent)w.ClientStream[3]).Position.Y);
            Assert.AreEqual("hero", ((EntityLabeledEvent)w.ClientStream[4]).Label);
        }

        [Test]
        public void Client_Stream_Sees_Events_Synchronously_On_Server_Add()
        {
            var w = NewWired();
            var seen = new List<int>();
            w.ClientStream.OnAppended += ev => seen.Add(((EntityCreatedEvent)ev).EntityId);

            w.ServerStream.Emit(new EntityCreatedEvent { EntityId = 10 });
            CollectionAssert.AreEqual(new int[] { 10 }, seen);

            w.ServerStream.Emit(new EntityCreatedEvent { EntityId = 20 });
            CollectionAssert.AreEqual(new int[] { 10, 20 }, seen);
        }
    }
}
