using Framework.Wire;
using NUnit.Framework;
using WireBridge = Framework.Wire.Wire;

namespace Framework.Tags.Tests
{
    public class WireRoundTripTests
    {
        static (EventStream server, EventStream client, Network network) Wired()
        {
            var server = new EventStream();
            var client = new EventStream();
            var network = new Network();
            new WireBridge(server, client, network);
            return (server, client, network);
        }

        [Test]
        public void TagSet_Int_RoundTrips_Through_Wire()
        {
            var (server, client, network) = Wired();
            var serverBag = new TagBagProbe { EntityId = 1, Emit = server.Add };
            var clientBag = new TagBagProbe { EntityId = 1 };

            network.Register<TagSetEvent<int>>().OnReceived += clientBag.Apply;

            serverBag.Set(SampleTags.Health, 30);
            network.Pump();

            Assert.AreEqual(1, client.Count);
            var rx = (TagSetEvent<int>)client[0];
            Assert.AreEqual(SampleTags.Health.Slot, rx.Slot);
            Assert.AreEqual(30, rx.Value);
            Assert.AreEqual(30, clientBag.Get(SampleTags.Health));
        }

        [Test]
        public void TagSet_Bool_RoundTrips_Through_Wire()
        {
            var (server, client, network) = Wired();
            var serverBag = new TagBagProbe { EntityId = 1, Emit = server.Add };
            var clientBag = new TagBagProbe { EntityId = 1 };

            network.Register<TagSetEvent<bool>>().OnReceived += clientBag.Apply;

            serverBag.Set(SampleTags.IsCard, true);
            network.Pump();

            Assert.AreEqual(true, clientBag.Get(SampleTags.IsCard));
        }

        [Test]
        public void TagRemoved_RoundTrips_And_Clears_Slot()
        {
            var (server, client, network) = Wired();
            var serverBag = new TagBagProbe { EntityId = 1, Emit = server.Add };
            var clientBag = new TagBagProbe { EntityId = 1 };

            network.Register<TagSetEvent<int>>() .OnReceived += clientBag.Apply;
            network.Register<TagRemovedEvent>()  .OnReceived += clientBag.Apply;

            serverBag.Set(SampleTags.Health, 30);
            serverBag.Remove(SampleTags.Health);
            network.Pump();

            Assert.IsFalse(clientBag.Has(SampleTags.Health));
            Assert.IsInstanceOf<TagRemovedEvent>(client[1]);
        }

        [Test]
        public void Mixed_Channels_Preserve_Send_Order()
        {
            var (server, client, network) = Wired();
            var serverBag = new TagBagProbe { EntityId = 1, Emit = server.Add };
            var clientBag = new TagBagProbe { EntityId = 1 };

            network.Register<TagSetEvent<int>>() .OnReceived += clientBag.Apply;
            network.Register<TagSetEvent<bool>>().OnReceived += clientBag.Apply;

            serverBag.Set(SampleTags.Health, 10);
            serverBag.Set(SampleTags.IsCard, true);
            serverBag.Set(SampleTags.Attack, 5);
            network.Pump();

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
