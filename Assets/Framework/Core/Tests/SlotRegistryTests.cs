using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Framework.Core.Tests
{
    public class SlotRegistryTests
    {
        abstract record Kind(int Slot) : TypeKey(Slot);
        sealed record Alpha() : Kind(1);
        sealed record Beta()  : Kind(2);

        [Test]
        public void RegisterByType_ResolvesBySlotAndType()
        {
            var reg = new SlotRegistry<Kind>();
            reg.Register<Alpha>();

            Assert.IsInstanceOf<Alpha>(reg.BySlot(1));
            Assert.IsInstanceOf<Alpha>(reg.Of<Alpha>());
            Assert.AreSame(reg.BySlot(1), reg.Of<Alpha>(), "type and slot paths resolve the same instance");
        }

        [Test]
        public void RegisterByInstance_ResolvesBySlot_ButNotByType()
        {
            var reg = new SlotRegistry<Kind>();
            reg.Register(new Beta());   // data path: identity only

            Assert.IsInstanceOf<Beta>(reg.BySlot(2));
            Assert.Throws<KeyNotFoundException>(() => reg.Of<Beta>(),
                "instance-registered members have no compile-time type entry");
        }

        [Test]
        public void DuplicateSlot_Throws()
        {
            var reg = new SlotRegistry<Kind>();
            reg.Register<Alpha>();

            Assert.Throws<InvalidOperationException>(() => reg.Register(new Alpha()),
                "slot 1 is already claimed");
        }

        [Test]
        public void TryBySlot_HitReturnsTrue_MissReturnsFalse()
        {
            var reg = new SlotRegistry<Kind>();
            reg.Register<Alpha>();

            Assert.IsTrue(reg.TryBySlot(1, out var hit));
            Assert.IsInstanceOf<Alpha>(hit);

            Assert.IsFalse(reg.TryBySlot(99, out var miss));
            Assert.IsNull(miss);
        }
    }
}
