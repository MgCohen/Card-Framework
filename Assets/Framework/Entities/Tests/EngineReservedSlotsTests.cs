using NUnit.Framework;

namespace Framework.Entities.Tests
{
    public class EngineReservedSlotsTests
    {
        [Test]
        public void EngineReserved_SlotsMatchClosure()
        {
            Assert.AreEqual(0, EntityTypes.Unknown.Slot);
            Assert.AreEqual(2, EntityTypes.Player.Slot);
            Assert.AreEqual(3, EntityTypes.Zone.Slot);
            Assert.AreEqual(4, EntityTypes.Card.Slot);
            Assert.AreEqual(5, EntityTypes.Modifier.Slot);
        }

        [Test]
        public void EngineReserved_RegisteredInRegistry()
        {
            _ = EntityTypes.Card;

            Assert.AreEqual(EntityTypes.Unknown,  EntityTypeRegistry.BySlot(0));
            Assert.AreEqual(EntityTypes.Player,   EntityTypeRegistry.BySlot(2));
            Assert.AreEqual(EntityTypes.Zone,     EntityTypeRegistry.BySlot(3));
            Assert.AreEqual(EntityTypes.Card,     EntityTypeRegistry.BySlot(4));
            Assert.AreEqual(EntityTypes.Modifier, EntityTypeRegistry.BySlot(5));
        }

        [Test]
        public void EngineReserved_NamesReadable()
        {
            Assert.AreEqual("Unknown",  EntityTypes.Unknown.Name);
            Assert.AreEqual("Player",   EntityTypes.Player.Name);
            Assert.AreEqual("Zone",     EntityTypes.Zone.Name);
            Assert.AreEqual("Card",     EntityTypes.Card.Name);
            Assert.AreEqual("Modifier", EntityTypes.Modifier.Name);
        }

        [Test]
        public void Slot1_IsReserved_NotPreRegistered()
        {
            Assert.IsFalse(EntityTypeRegistry.TryGetBySlot(1, out _));
        }
    }
}
