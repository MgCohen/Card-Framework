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
            var w = WireBridge.Connect();
            w.ServerNetwork.Register<EntityCreatedEvent>();
            w.ClientNetwork.Register<EntityCreatedEvent>();

            w.ServerStream.Emit(new EntityCreatedEvent
            {
                EntityId     = 7,
                TypeSlot     = EntityTypes.Card.Slot,
                DefinitionId = 101,
            });

            Assert.AreEqual(1, w.ClientStream.Count);
            var rx = (EntityCreatedEvent)w.ClientStream[0];
            Assert.AreEqual(7, rx.EntityId);
            Assert.AreEqual(EntityTypes.Card, EntityTypeRegistry.BySlot(rx.TypeSlot));
            Assert.AreEqual(101, rx.DefinitionId);
        }

        [Test]
        public void EntityDestroyedEvent_RoundTripsThroughWire()
        {
            var w = WireBridge.Connect();
            w.ServerNetwork.Register<EntityDestroyedEvent>();
            w.ClientNetwork.Register<EntityDestroyedEvent>();

            w.ServerStream.Emit(new EntityDestroyedEvent { EntityId = 42 });

            Assert.AreEqual(1, w.ClientStream.Count);
            Assert.AreEqual(42, ((EntityDestroyedEvent)w.ClientStream[0]).EntityId);
        }
    }
}
