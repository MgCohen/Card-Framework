#nullable enable
using System;
using System.Buffers.Binary;
using TypedPayloads.Engine;

namespace TypedPayloads.Sample
{
    /// <summary>
    /// Hand-written codec installer. This is the shape the future source
    /// generator will emit automatically — one <see cref="ITagCodec{T}"/>
    /// per declared <see cref="TagKey{T}"/>, all registered into
    /// <see cref="TagCodecs"/> at startup.
    ///
    /// <para>Call <see cref="Install"/> exactly once at bootstrap. Idempotent
    /// — extra calls are no-ops, so tests can call it from
    /// <c>OneTimeSetUp</c> without coordinating with other consumers.</para>
    /// </summary>
    public static class Codecs
    {
        private static bool _installed;

        public static void Install()
        {
            if (_installed) return;
            _installed = true;

            TagCodecs.Register(Tags.Health,   new IntCodec());
            TagCodecs.Register(Tags.IsMinion, new BoolCodec());
            TagCodecs.Register(Tags.Pos,      new Vector3Codec());
        }

        // ---- Primitive codecs — source-gen output for int/bool/long/enum ----

        public sealed class IntCodec : TagCodec<int>
        {
            public override TagValue Encode(int v)      => TagValue.I64(v);
            public override int      Decode(TagValue w) => (int)w.IntValue;
        }

        public sealed class BoolCodec : TagCodec<bool>
        {
            public override TagValue Encode(bool v)     => TagValue.I64(v ? 1 : 0);
            public override bool     Decode(TagValue w) => w.IntValue != 0;
        }

        // ---- Custom-struct codec — source-gen output from [WireSerializable] ----

        public sealed class Vector3Codec : TagCodec<Vector3>
        {
            // Bit-cast float ↔ int32 then write little-endian. The Single*
            // BinaryPrimitives overloads are .NET 5+, not present on Unity's
            // .NET Standard 2.1 surface, but Int32* + BitConverter bit-cast
            // are 2.1 and produce identical bytes.
            public override TagValue Encode(Vector3 v)
            {
                var buf = new byte[12];
                var s   = buf.AsSpan();
                BinaryPrimitives.WriteInt32LittleEndian(s,           BitConverter.SingleToInt32Bits(v.X));
                BinaryPrimitives.WriteInt32LittleEndian(s.Slice(4),  BitConverter.SingleToInt32Bits(v.Y));
                BinaryPrimitives.WriteInt32LittleEndian(s.Slice(8),  BitConverter.SingleToInt32Bits(v.Z));
                return TagValue.Bytes(buf);
            }
            public override Vector3 Decode(TagValue w)
            {
                var s = w.BytesValue.Span;
                return new Vector3(
                    BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(s)),
                    BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(s.Slice(4))),
                    BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(s.Slice(8))));
            }
        }
    }
}
