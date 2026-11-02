using NUnit.Framework;

namespace Framework.RecordIds.Tests
{
    public class IdPackingTests
    {
        [Test]
        public void IdField_RoundTrips_AsEqualId()
        {
            var source = new IdHolder { A = new AId { Value = 7 }, B = new BId { Value = 9 } };

            var back = new IdHolder(source.PackTyped());

            Assert.AreEqual(new AId { Value = 7 }, back.A);
            Assert.AreEqual(new BId { Value = 9 }, back.B);
        }

        [Test]
        public void NullField_PacksMinusOne_AndUnpacksNull()
        {
            var packed = new IdHolder { A = null, B = null }.PackTyped();

            Assert.AreEqual(-1, packed.A.Value);
            var back = new IdHolder(packed);
            Assert.IsNull(back.A);
            Assert.IsNull(back.B);
        }

        [Test]
        public void ZeroValue_UnpacksAsIdWithZero()
        {
            var back = new IdHolder(new IdHolder { A = new AId { Value = 0 } }.PackTyped());

            Assert.IsNotNull(back.A);
            Assert.AreEqual(0, back.A.Value);
        }
    }
}
