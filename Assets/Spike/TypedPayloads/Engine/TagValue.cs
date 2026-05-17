#nullable enable
using System;

namespace TypedPayloads.Engine
{
    /// <summary>
    /// The two wire-level value variants: <see cref="K.I64"/> for everything
    /// that fits in a 64-bit integer (bool/int/long/enum/entity-id), and
    /// <see cref="K.Bytes"/> for custom types decoded by the per-slot codec.
    /// Maps directly to protobuf <c>oneof { int64 i; bytes b; }</c>.
    ///
    /// <para>This is the only place wire-level polymorphism lives. The set of
    /// kinds is closed and framework-controlled — downstream consumers extend
    /// the space of declarable tag value types via <see cref="ITagCodec{T}"/>,
    /// not by adding new variants here.</para>
    /// </summary>
    public readonly struct TagValue : IEquatable<TagValue>
    {
        public enum K : byte { I64, Bytes }

        public readonly K                    Kind;
        public readonly long                 IntValue;
        public readonly ReadOnlyMemory<byte> BytesValue;

        private TagValue(K k, long i, ReadOnlyMemory<byte> b)
        { Kind = k; IntValue = i; BytesValue = b; }

        public static TagValue I64  (long v)                 => new(K.I64,   v, default);
        public static TagValue Bytes(ReadOnlyMemory<byte> b) => new(K.Bytes, 0, b);

        public bool Equals(TagValue other)
        {
            if (Kind != other.Kind) return false;
            return Kind switch
            {
                K.I64   => IntValue == other.IntValue,
                K.Bytes => BytesValue.Span.SequenceEqual(other.BytesValue.Span),
                _       => false,
            };
        }
        public override bool Equals(object? obj) => obj is TagValue tv && Equals(tv);
        public override int  GetHashCode() => Kind switch
        {
            K.I64   => IntValue.GetHashCode(),
            K.Bytes => BytesValue.Length,
            _       => 0,
        };
        public override string ToString() => Kind switch
        {
            K.I64   => $"i64({IntValue})",
            K.Bytes => $"bytes[{BytesValue.Length}]",
            _       => "?",
        };
    }
}
