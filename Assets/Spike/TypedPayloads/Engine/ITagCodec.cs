#nullable enable
namespace TypedPayloads.Engine
{
    /// <summary>
    /// Non-generic codec entry point — used by the wire-emit path
    /// (<see cref="EntityStore"/>) where the value is type-erased <c>object</c>
    /// because <c>TagKey</c> doesn't carry its <c>T</c> at the event-fire
    /// site.
    /// </summary>
    public interface ITagCodec
    {
        TagValue EncodeObject(object value);
        object   DecodeObject(TagValue wire);
    }

    /// <summary>
    /// Typed codec — used by consumers reading values back out of a
    /// <see cref="MirrorEntity"/> via <see cref="TagKey{T}"/>.
    /// </summary>
    public interface ITagCodec<T> : ITagCodec
    {
        TagValue Encode(T value);
        T        Decode(TagValue wire);
    }

    /// <summary>
    /// Convenience base. Implements the non-generic methods by delegating to
    /// the typed ones — codec authors only override <see cref="Encode"/> and
    /// <see cref="Decode"/>.
    /// </summary>
    public abstract class TagCodec<T> : ITagCodec<T>
    {
        public abstract TagValue Encode(T value);
        public abstract T        Decode(TagValue wire);

        public TagValue EncodeObject(object value) => Encode((T)value);
        public object   DecodeObject(TagValue wire) => Decode(wire)!;
    }
}
