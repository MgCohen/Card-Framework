using NUnit.Framework;

namespace Framework.Entities.Tests
{
    public class EntityTypeTests
    {
        [Test]
        public void Construction_SetsSlotAndName()
        {
            var type = new EntityType(42, "Test");
            Assert.AreEqual(42, type.Slot);
            Assert.AreEqual("Test", type.Name);
        }

        [Test]
        public void Equality_ComparesOnSlotOnly()
        {
            var a = new EntityType(7, "Alpha");
            var b = new EntityType(7, "Beta");
            var c = new EntityType(8, "Alpha");

            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, c);
        }

        [Test]
        public void GetHashCode_UsesSlotOnly()
        {
            var a = new EntityType(7, "Alpha");
            var b = new EntityType(7, "Beta");
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        }

        [Test]
        public void ToString_UsesName()
        {
            var type = new EntityType(42, "Test");
            Assert.AreEqual("Test", type.ToString());
        }
    }
}
