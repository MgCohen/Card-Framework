using Framework.Tags;
using Framework.Wire;
using NUnit.Framework;
using WireBridge = Framework.Wire.Wire;

namespace Framework.Entities.Tests
{
    public class WireRoundTripTests
    {
        [Test]
        public void TagSet_Int_RoundTrips_Through_Wire()
        {
            var (server, client, serverNet, clientNet) = WireBridge.Connect();
            serverNet.Register<TagSetEvent<int>>();
            var clientChannel = clientNet.Register<TagSetEvent<int>>();

            var serverEntity = new EntityStore().Create(EntityTypes.Card);
            var clientEntity = new EntityStore().Create(serverEntity.Id, EntityTypes.Card, 0);
            clientChannel.OnReceived += ev => clientEntity.Set((TagKey<int>)TagRegistry.BySlot(ev.Slot), ev.Value);

            serverEntity.Set(SampleTags.Health, 30);
            server.Add(new TagSetEvent<int>
            {
                EntityId = serverEntity.Id,
                Slot     = SampleTags.Health.Slot,
                Value    = 30,
            });

            Assert.AreEqual(1, client.Count);
            var rx = (TagSetEvent<int>)client[0];
            Assert.AreEqual(SampleTags.Health.Slot, rx.Slot);
            Assert.AreEqual(30, rx.Value);
            Assert.AreEqual(30, clientEntity.Get(SampleTags.Health));
        }

        [Test]
        public void TagSet_Bool_RoundTrips_Through_Wire()
        {
            var (server, client, serverNet, clientNet) = WireBridge.Connect();
            serverNet.Register<TagSetEvent<bool>>();
            var clientChannel = clientNet.Register<TagSetEvent<bool>>();

            var serverEntity = new EntityStore().Create(EntityTypes.Card);
            var clientEntity = new EntityStore().Create(serverEntity.Id, EntityTypes.Card, 0);
            clientChannel.OnReceived += ev => clientEntity.Set((TagKey<bool>)TagRegistry.BySlot(ev.Slot), ev.Value);

            serverEntity.Set(SampleTags.IsCard, true);
            server.Add(new TagSetEvent<bool>
            {
                EntityId = serverEntity.Id,
                Slot     = SampleTags.IsCard.Slot,
                Value    = true,
            });

            Assert.AreEqual(true, clientEntity.Get(SampleTags.IsCard));
        }

        [Test]
        public void TagRemoved_RoundTrips_And_Clears_Slot()
        {
            var (server, client, serverNet, clientNet) = WireBridge.Connect();
            serverNet.Register<TagSetEvent<int>>();
            serverNet.Register<TagRemovedEvent>();
            var clientSetChannel = clientNet.Register<TagSetEvent<int>>();
            var clientRemoveChannel = clientNet.Register<TagRemovedEvent>();

            var serverEntity = new EntityStore().Create(EntityTypes.Card);
            var clientEntity = new EntityStore().Create(serverEntity.Id, EntityTypes.Card, 0);
            clientSetChannel.OnReceived += ev => clientEntity.Set((TagKey<int>)TagRegistry.BySlot(ev.Slot), ev.Value);
            clientRemoveChannel.OnReceived += ev => clientEntity.Remove(TagRegistry.BySlot(ev.Slot));

            serverEntity.Set(SampleTags.Health, 30);
            server.Add(new TagSetEvent<int>
            {
                EntityId = serverEntity.Id,
                Slot     = SampleTags.Health.Slot,
                Value    = 30,
            });
            serverEntity.Remove(SampleTags.Health);
            server.Add(new TagRemovedEvent
            {
                EntityId = serverEntity.Id,
                Slot     = SampleTags.Health.Slot,
            });

            Assert.IsFalse(clientEntity.Has(SampleTags.Health));
            Assert.IsInstanceOf<TagRemovedEvent>(client[1]);
        }

        [Test]
        public void Mixed_Channels_Preserve_Send_Order()
        {
            var (server, client, serverNet, clientNet) = WireBridge.Connect();
            serverNet.Register<TagSetEvent<int>>();
            serverNet.Register<TagSetEvent<bool>>();
            var clientIntChannel = clientNet.Register<TagSetEvent<int>>();
            var clientBoolChannel = clientNet.Register<TagSetEvent<bool>>();

            var serverEntity = new EntityStore().Create(EntityTypes.Card);
            var clientEntity = new EntityStore().Create(serverEntity.Id, EntityTypes.Card, 0);
            clientIntChannel.OnReceived  += ev => clientEntity.Set((TagKey<int>) TagRegistry.BySlot(ev.Slot), ev.Value);
            clientBoolChannel.OnReceived += ev => clientEntity.Set((TagKey<bool>)TagRegistry.BySlot(ev.Slot), ev.Value);

            serverEntity.Set(SampleTags.Health, 10);
            server.Add(new TagSetEvent<int>  { EntityId = serverEntity.Id, Slot = SampleTags.Health.Slot, Value = 10 });
            serverEntity.Set(SampleTags.IsCard, true);
            server.Add(new TagSetEvent<bool> { EntityId = serverEntity.Id, Slot = SampleTags.IsCard.Slot, Value = true });
            serverEntity.Set(SampleTags.Attack, 5);
            server.Add(new TagSetEvent<int>  { EntityId = serverEntity.Id, Slot = SampleTags.Attack.Slot, Value = 5 });

            Assert.AreEqual(3, client.Count);
            Assert.IsInstanceOf<TagSetEvent<int>> (client[0]);
            Assert.IsInstanceOf<TagSetEvent<bool>>(client[1]);
            Assert.IsInstanceOf<TagSetEvent<int>> (client[2]);
            Assert.AreEqual(10,   clientEntity.Get(SampleTags.Health));
            Assert.AreEqual(true, clientEntity.Get(SampleTags.IsCard));
            Assert.AreEqual(5,    clientEntity.Get(SampleTags.Attack));
        }
    }
}
