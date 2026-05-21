using Framework.Wire;
using NUnit.Framework;
using WireBridge = Framework.Wire.Wire;

namespace Framework.Entities.Tests
{
    public class EntityWireRoundTripTests
    {
        [Test]
        public void EntityCreatedEvent_FromEntity_RoundTripsThroughWire()
        {
            var w = WireBridge.Connect();
            w.ServerNetwork.Register<EntityCreatedEvent>();
            w.ClientNetwork.Register<EntityCreatedEvent>();

            var store = new EntityStore();
            var entity = store.From(new CardTestDef(101, 3));

            w.ServerStream.Emit(new EntityCreatedEvent
            {
                EntityId     = entity.Id,
                TypeSlot     = entity.Type.Slot,
                DefinitionId = entity.DefinitionId,
            });

            Assert.AreEqual(1, w.ClientStream.Count);
            var rx = (EntityCreatedEvent)w.ClientStream[0];
            Assert.AreEqual(entity.Id, rx.EntityId);
            Assert.AreEqual(entity.Type, EntityTypeRegistry.BySlot(rx.TypeSlot));
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
