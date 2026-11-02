using NUnit.Framework;

namespace Framework.RecordIds.Tests
{
    public class IdTests
    {
        [Test]
        public void SameType_SameValue_AreEqual()
        {
            Assert.AreEqual(new AId { Value = 5 }, new AId { Value = 5 });
            Assert.IsTrue(new AId { Value = 5 } == new AId { Value = 5 });
            Assert.AreEqual(new AId { Value = 5 }.GetHashCode(), new AId { Value = 5 }.GetHashCode());
        }

        [Test]
        public void SameType_DifferentValue_AreNotEqual()
        {
            Assert.AreNotEqual(new AId { Value = 5 }, new AId { Value = 6 });
        }

        [Test]
        public void DifferentType_SameValue_AreNotEqual()
        {
            Id a = new AId { Value = 5 };
            Id b = new BId { Value = 5 };

            Assert.AreNotEqual(a, b);
            Assert.IsFalse(a == b);
        }

        [Test]
        public void NewId_HasValueMinusOne()
        {
            Assert.AreEqual(-1, new AId().Value);
        }
    }
}
