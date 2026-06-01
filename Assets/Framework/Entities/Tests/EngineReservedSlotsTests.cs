using NUnit.Framework;

namespace Framework.Entities.Tests
{
    // The framework's reserved entity-kind slots and their presence in a baseline registry.
    // Slot 1 is intentionally left unclaimed.
    public class EngineReservedSlotsTests
    {
        [Test]
        public void EngineReserved_SlotsMatchClosure()
        {
            Assert.AreEqual(0, new Unknown().Slot);
            Assert.AreEqual(2, new Player().Slot);
            Assert.AreEqual(3, new Zone().Slot);
            Assert.AreEqual(4, new Card().Slot);
            Assert.AreEqual(5, new Modifier().Slot);
        }

        [Test]
        public void EngineReserved_RegisteredInBaselines()
        {
            var registry = EntityTypeRegistry.WithBaselines();

            Assert.AreEqual(new Unknown(),  registry.BySlot(0));
            Assert.AreEqual(new Player(),   registry.BySlot(2));
            Assert.AreEqual(new Zone(),     registry.BySlot(3));
            Assert.AreEqual(new Card(),     registry.BySlot(4));
            Assert.AreEqual(new Modifier(), registry.BySlot(5));
        }

        [Test]
        public void Slot1_IsReserved_NotPreRegistered()
        {
            var registry = EntityTypeRegistry.WithBaselines();
            Assert.IsFalse(registry.TryBySlot(1, out _));
        }
    }
}
