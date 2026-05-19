using Framework.Wire;
using NUnit.Framework;
using WireBridge = Framework.Wire.Wire;

namespace Framework.Tags.Tests
{
    public class WireRoundTripTests
    {
        [Test]
        public void TagSet_Int_RoundTrips_Through_Wire()
        {
            var (server, client, serverNet, clientNet) = WireBridge.Connect();
            serverNet.Register<TagSetEvent<int>>();
            var clientChannel = clientNet.Register<TagSetEvent<int>>();

            var serverBag = new TagBagProbe { EntityId = 1, Emit = server.Add };
            var clientBag = new TagBagProbe { EntityId = 1 };
            clientChannel.OnReceived += clientBag.Apply;

            serverBag.Set(SampleTags.Health, 30);

            Assert.AreEqual(1, client.Count);
            var rx = (TagSetEvent<int>)client[0];
            Assert.AreEqual(SampleTags.Health.Slot, rx.Slot);
            Assert.AreEqual(30, rx.Value);
            Assert.AreEqual(30, clientBag.Get(SampleTags.Health));
        }

        [Test]
        public void TagSet_Bool_RoundTrips_Through_Wire()
        {
            var (server, client, serverNet, clientNet) = WireBridge.Connect();
            serverNet.Register<TagSetEvent<bool>>();
            var clientChannel = clientNet.Register<TagSetEvent<bool>>();

            var serverBag = new TagBagProbe { EntityId = 1, Emit = server.Add };
            var clientBag = new TagBagProbe { EntityId = 1 };
            clientChannel.OnReceived += clientBag.Apply;

            serverBag.Set(SampleTags.IsCard, true);

            Assert.AreEqual(true, clientBag.Get(SampleTags.IsCard));
        }

        [Test]
        public void TagRemoved_RoundTrips_And_Clears_Slot()
        {
            var (server, client, serverNet, clientNet) = WireBridge.Connect();
            serverNet.Register<TagSetEvent<int>>();
            serverNet.Register<TagRemovedEvent>();
            var clientSetChannel = clientNet.Register<TagSetEvent<int>>();
            var clientRemoveChannel = clientNet.Register<TagRemovedEvent>();

            var serverBag = new TagBagProbe { EntityId = 1, Emit = server.Add };
            var clientBag = new TagBagProbe { EntityId = 1 };
            clientSetChannel.OnReceived += clientBag.Apply;
            clientRemoveChannel.OnReceived += clientBag.Apply;

            serverBag.Set(SampleTags.Health, 30);
            serverBag.Remove(SampleTags.Health);

            Assert.IsFalse(clientBag.Has(SampleTags.Health));
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

            var serverBag = new TagBagProbe { EntityId = 1, Emit = server.Add };
            var clientBag = new TagBagProbe { EntityId = 1 };
            clientIntChannel.OnReceived += clientBag.Apply;
            clientBoolChannel.OnReceived += clientBag.Apply;

            serverBag.Set(SampleTags.Health, 10);
            serverBag.Set(SampleTags.IsCard, true);
            serverBag.Set(SampleTags.Attack, 5);

            Assert.AreEqual(3, client.Count);
            Assert.IsInstanceOf<TagSetEvent<int>> (client[0]);
            Assert.IsInstanceOf<TagSetEvent<bool>>(client[1]);
            Assert.IsInstanceOf<TagSetEvent<int>> (client[2]);
            Assert.AreEqual(10,   clientBag.Get(SampleTags.Health));
            Assert.AreEqual(true, clientBag.Get(SampleTags.IsCard));
            Assert.AreEqual(5,    clientBag.Get(SampleTags.Attack));
        }
    }
}
