#nullable enable
using NUnit.Framework;
using TypedPayloads.Engine;

namespace TypedPayloads.Tests
{
    public class TagValueTests
    {
        [Test]
        public void I64_HoldsIntegerValue()
        {
            var v = TagValue.I64(42);
            Assert.AreEqual(TagValue.K.I64, v.Kind);
            Assert.AreEqual(42L, v.IntValue);
        }

        [Test]
        public void Bytes_HoldsByteArray()
        {
            var data = new byte[] { 1, 2, 3, 4 };
            var v = TagValue.Bytes(data);
            Assert.AreEqual(TagValue.K.Bytes, v.Kind);
            Assert.AreEqual(4, v.BytesValue.Length);
            CollectionAssert.AreEqual(data, v.BytesValue.ToArray());
        }

        [Test]
        public void Equality_SameKindAndValue_AreEqual()
        {
            Assert.AreEqual(TagValue.I64(7), TagValue.I64(7));
        }

        [Test]
        public void Equality_DifferentIntValues_AreNotEqual()
        {
            Assert.AreNotEqual(TagValue.I64(7), TagValue.I64(8));
        }

        [Test]
        public void Equality_DifferentKinds_AreNotEqual()
        {
            Assert.AreNotEqual(TagValue.I64(0), TagValue.Bytes(new byte[] { 0 }));
        }

        [Test]
        public void Equality_Bytes_ComparesByContent()
        {
            var a = TagValue.Bytes(new byte[] { 1, 2, 3 });
            var b = TagValue.Bytes(new byte[] { 1, 2, 3 });
            var c = TagValue.Bytes(new byte[] { 1, 2, 4 });
            Assert.AreEqual(a, b);
            Assert.AreNotEqual(a, c);
        }
    }
}
