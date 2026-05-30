using Framework.Tags;
using Framework.Wire;
using NUnit.Framework;
using WireBridge = Framework.Wire.Wire;

namespace Framework.Entities.Tests
{
    // Wire-layer round-trip for tag events. These tests prove serialization survives
    // through Network/NetworkChannel/Dispatcher. The end-to-end "server mutates →
    // client mirrors" flow including handlers lives in MirrorIntegrationTests.
    public class WireRoundTripTests
    {
        [Test]
        public void TagSet_Int_RoundTrips_Through_Wire()
        {
            var w = WireBridge.Connect();
            w.ServerNetwork.Register<TagSetEvent<int>, TagSetEvent<int>.Packed>();
            w.ClientNetwork.Register<TagSetEvent<int>, TagSetEvent<int>.Packed>();

            w.ServerStream.Emit(new TagSetEvent<int>
            {
                EntityId = 1,
                Slot     = SampleTags.Health.Slot,
                Value    = 30,
            });

            Assert.AreEqual(1, w.ClientStream.Count);
            var rx = (TagSetEvent<int>)w.ClientStream[0];
            Assert.AreEqual(1, rx.EntityId);
            Assert.AreEqual(SampleTags.Health.Slot, rx.Slot);
            Assert.AreEqual(30, rx.Value);
        }

        [Test]
        public void TagSet_Bool_RoundTrips_Through_Wire()
        {
            var w = WireBridge.Connect();
            w.ServerNetwork.Register<TagSetEvent<bool>, TagSetEvent<bool>.Packed>();
            w.ClientNetwork.Register<TagSetEvent<bool>, TagSetEvent<bool>.Packed>();

            w.ServerStream.Emit(new TagSetEvent<bool>
            {
                EntityId = 1,
                Slot     = SampleTags.IsCard.Slot,
                Value    = true,
            });

            Assert.AreEqual(1, w.ClientStream.Count);
            var rx = (TagSetEvent<bool>)w.ClientStream[0];
            Assert.AreEqual(true, rx.Value);
        }

        [Test]
        public void TagChange_RoundTrips_Through_Wire()
        {
            var w = WireBridge.Connect();
            w.ServerNetwork.Register<TagChangeEvent<int>, TagChangeEvent<int>.Packed>();
            w.ClientNetwork.Register<TagChangeEvent<int>, TagChangeEvent<int>.Packed>();

            w.ServerStream.Emit(new TagChangeEvent<int>
            {
                EntityId = 1,
                Slot     = SampleTags.Health.Slot,
                Prev     = 30,
                Next     = 25,
            });

            Assert.AreEqual(1, w.ClientStream.Count);
            var rx = (TagChangeEvent<int>)w.ClientStream[0];
            Assert.AreEqual(30, rx.Prev);
            Assert.AreEqual(25, rx.Next);
        }

        [Test]
        public void TagRemoved_RoundTrips_Through_Wire()
        {
            var w = WireBridge.Connect();
            w.ServerNetwork.Register<TagRemovedEvent, TagRemovedEvent.Packed>();
            w.ClientNetwork.Register<TagRemovedEvent, TagRemovedEvent.Packed>();

            w.ServerStream.Emit(new TagRemovedEvent
            {
                EntityId = 1,
                Slot     = SampleTags.Health.Slot,
            });

            Assert.AreEqual(1, w.ClientStream.Count);
            var rx = (TagRemovedEvent)w.ClientStream[0];
            Assert.AreEqual(1, rx.EntityId);
            Assert.AreEqual(SampleTags.Health.Slot, rx.Slot);
        }

        [Test]
        public void Mixed_Channels_Preserve_Send_Order()
        {
            var w = WireBridge.Connect();
            w.ServerNetwork.Register<TagSetEvent<int>, TagSetEvent<int>.Packed>();
            w.ServerNetwork.Register<TagSetEvent<bool>, TagSetEvent<bool>.Packed>();
            w.ClientNetwork.Register<TagSetEvent<int>, TagSetEvent<int>.Packed>();
            w.ClientNetwork.Register<TagSetEvent<bool>, TagSetEvent<bool>.Packed>();

            w.ServerStream.Emit(new TagSetEvent<int>  { EntityId = 1, Slot = SampleTags.Health.Slot, Value = 10 });
            w.ServerStream.Emit(new TagSetEvent<bool> { EntityId = 1, Slot = SampleTags.IsCard.Slot, Value = true });
            w.ServerStream.Emit(new TagSetEvent<int>  { EntityId = 1, Slot = SampleTags.Attack.Slot, Value = 5 });

            Assert.AreEqual(3, w.ClientStream.Count);
            Assert.IsInstanceOf<TagSetEvent<int>> (w.ClientStream[0]);
            Assert.IsInstanceOf<TagSetEvent<bool>>(w.ClientStream[1]);
            Assert.IsInstanceOf<TagSetEvent<int>> (w.ClientStream[2]);
        }
    }
}
