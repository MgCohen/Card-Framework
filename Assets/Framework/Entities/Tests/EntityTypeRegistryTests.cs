using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Framework.Entities.Tests
{
    public class EntityTypeRegistryTests
    {
        [Test]
        public void Register_ReturnsTheNewEntityType()
        {
            var type = EntityTypeRegistry.Register(8000, "TestType8000");
            Assert.AreEqual(8000, type.Slot);
            Assert.AreEqual("TestType8000", type.Name);
        }

        [Test]
        public void BySlot_ReturnsRegisteredInstance()
        {
            var registered = EntityTypeRegistry.Register(8001, "TestType8001");
            Assert.AreEqual(registered, EntityTypeRegistry.BySlot(8001));
        }

        [Test]
        public void Register_DuplicateSlot_Throws()
        {
            EntityTypeRegistry.Register(8002, "First8002");
            Assert.Throws<InvalidOperationException>(() => EntityTypeRegistry.Register(8002, "Second8002"));
        }

        [Test]
        public void BySlot_UnregisteredSlot_Throws()
        {
            Assert.Throws<KeyNotFoundException>(() => EntityTypeRegistry.BySlot(9999));
        }

        [Test]
        public void TryGetBySlot_ReturnsTrue_ForRegistered()
        {
            EntityTypeRegistry.Register(8003, "TestType8003");
            Assert.IsTrue(EntityTypeRegistry.TryGetBySlot(8003, out _));
        }

        [Test]
        public void TryGetBySlot_ReturnsFalse_ForUnregistered()
        {
            Assert.IsFalse(EntityTypeRegistry.TryGetBySlot(9998, out _));
        }
    }
}
