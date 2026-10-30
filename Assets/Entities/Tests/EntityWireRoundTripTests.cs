using Framework.Wire;
using NUnit.Framework;
using WireBridge = Framework.Wire.Wire;

namespace Framework.Entities.Tests
{
    // Wire-layer round-trip for entity events. Pure serialization assertions; the full
    // server→client mirror via EntityService + EntityCreatedHandler lives in
    // MirrorIntegrationTests.
    public class EntityWireRoundTripTests
    {
        [Test]
        public void EntityCreatedEvent_RoundTripsThroughWire()
        {
            var types = EntityTypeRegistry.WithBaselines();
            var w = WireBridge.Connect();
            w.ServerNetwork.Register<EntityCreatedEvent, EntityCreatedEvent.Packed>();
            w.ClientNetwork.Register<EntityCreatedEvent, EntityCreatedEvent.Packed>();

            w.ServerStream.Emit(new EntityCreatedEvent
            {
                EntityId     = 7,
                TypeSlot     = new Card().Slot,
                DefinitionId = 101,
            });

            Assert.AreEqual(1, w.ClientStream.Count);
            var rx = (EntityCreatedEvent)w.ClientStream[0];
            Assert.AreEqual(7, rx.EntityId);
            Assert.AreEqual(new Card(), types.BySlot(rx.TypeSlot));
            Assert.AreEqual(101, rx.DefinitionId);
        }
    }
}
