#nullable enable
using TypedPayloads.Engine;

namespace TypedPayloads.Sample
{
    /// <summary>
    /// Sample custom struct demonstrating a <see cref="WireSerializableAttribute"/>
    /// tag value type. Packs three floats through the <c>Bytes</c> variant of
    /// <see cref="TagValue"/> via <see cref="Codecs.Vector3Codec"/>.
    ///
    /// <para>Defined locally (not <c>UnityEngine.Vector3</c>) so the spike
    /// stays runtime-independent and headless-testable.</para>
    /// </summary>
    [WireSerializable]
    public readonly struct Vector3
    {
        public readonly float X, Y, Z;

        public Vector3(float x, float y, float z) { X = x; Y = y; Z = z; }

        public override bool Equals(object? obj)
            => obj is Vector3 v && v.X == X && v.Y == Y && v.Z == Z;

        public override int GetHashCode()
            => (X.GetHashCode() * 397 ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode();

        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
