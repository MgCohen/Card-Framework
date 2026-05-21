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
            var (server, client, serverNet, clientNet) = WireBridge.Connect();
            serverNet.Register<EntityCreatedEvent>();
            clientNet.Register<EntityCreatedEvent>();

            var store = new EntityStore();
            var entity = store.From(new CardTestDef(101, 3));

            server.Emit(new EntityCreatedEvent
            {
                EntityId     = entity.Id,
                TypeSlot     = entity.Type.Slot,
                DefinitionId = entity.DefinitionId,
            });

            Assert.AreEqual(1, client.Count);
            var rx = (EntityCreatedEvent)client[0];
            Assert.AreEqual(entity.Id, rx.EntityId);
            Assert.AreEqual(entity.Type, EntityTypeRegistry.BySlot(rx.TypeSlot));
            Assert.AreEqual(101, rx.DefinitionId);
        }

        [Test]
        public void EntityDestroyedEvent_RoundTripsThroughWire()
        {
            var (server, client, serverNet, clientNet) = WireBridge.Connect();
            serverNet.Register<EntityDestroyedEvent>();
            clientNet.Register<EntityDestroyedEvent>();

            server.Emit(new EntityDestroyedEvent { EntityId = 42 });

            Assert.AreEqual(1, client.Count);
            Assert.AreEqual(42, ((EntityDestroyedEvent)client[0]).EntityId);
        }
    }
}
