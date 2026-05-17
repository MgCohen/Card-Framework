#nullable enable
using NUnit.Framework;
using TypedPayloads.Engine;
using TypedPayloads.Sample;

namespace TypedPayloads.Tests
{
    public class CodecRoundtripTests
    {
        [OneTimeSetUp]
        public void Setup() => Codecs.Install();

        // --- Primitive variants ---------------------------------------------

        [Test]
        public void Int_RoundtripsThroughI64()
        {
            var codec = TagCodecs.Get(Tags.Health);
            var wire  = codec.Encode(42);
            Assert.AreEqual(TagValue.K.I64, wire.Kind);
            Assert.AreEqual(42, codec.Decode(wire));
        }

        [Test]
        public void Int_NegativeRoundtrips()
        {
            var codec = TagCodecs.Get(Tags.Health);
            Assert.AreEqual(-13, codec.Decode(codec.Encode(-13)));
        }

        [Test]
        public void Bool_True_EncodesAsOne()
        {
            var codec = TagCodecs.Get(Tags.IsMinion);
            var wire  = codec.Encode(true);
            Assert.AreEqual(TagValue.K.I64, wire.Kind);
            Assert.AreEqual(1L, wire.IntValue);
            Assert.IsTrue(codec.Decode(wire));
        }

        [Test]
        public void Bool_False_EncodesAsZero()
        {
            var codec = TagCodecs.Get(Tags.IsMinion);
            var wire  = codec.Encode(false);
            Assert.AreEqual(0L, wire.IntValue);
            Assert.IsFalse(codec.Decode(wire));
        }

        // --- Custom struct via Bytes variant --------------------------------

        [Test]
        public void Vector3_EncodesToBytesVariant()
        {
            var codec = TagCodecs.Get(Tags.Pos);
            var wire  = codec.Encode(new Vector3(1.0f, 2.0f, 3.0f));
            Assert.AreEqual(TagValue.K.Bytes, wire.Kind);
            Assert.AreEqual(12, wire.BytesValue.Length);  // 3 × float32
        }

        [Test]
        public void Vector3_RoundtripsThroughBytes()
        {
            var codec = TagCodecs.Get(Tags.Pos);
            var orig  = new Vector3(1.5f, -2.25f, 3.75f);
            var wire  = codec.Encode(orig);
            Assert.AreEqual(orig, codec.Decode(wire));
        }

        // --- Type-erased pipeline path --------------------------------------

        [Test]
        public void TypeErasedEncode_ForwardsThroughBoxing()
        {
            // EntityStore goes through TagCodecs.Encode(TagKey, object)
            // because its tag-event signature is type-erased. Verify that
            // path agrees with the typed Encode.
            object boxed = 99;
            var wire = TagCodecs.Encode(Tags.Health, boxed);
            Assert.AreEqual(TagValue.K.I64, wire.Kind);
            Assert.AreEqual(99L, wire.IntValue);
        }

        [Test]
        public void TypeErasedEncode_WorksForCustomStruct()
        {
            object boxed = new Vector3(7, 8, 9);
            var wire = TagCodecs.Encode(Tags.Pos, boxed);
            Assert.AreEqual(TagValue.K.Bytes, wire.Kind);
            Assert.AreEqual(new Vector3(7, 8, 9), TagCodecs.Get(Tags.Pos).Decode(wire));
        }

        [Test]
        public void UnregisteredSlot_Throws()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => TagCodecs.GetUntyped(slot: 99999));
        }
    }
}
