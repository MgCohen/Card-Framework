#nullable enable
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Engine.Entities
{
    /// <summary>
    /// Read-only view of an entity. Effects and hooks receive entities as
    /// <see cref="IEntity"/> (via their respective scopes), which type-enforces
    /// the read-only constraint — the compiler will not allow mutation
    /// through this surface. The mutable <see cref="Entity"/> implementation
    /// is reachable only through <c>ICommandScope.Resolve</c> or the
    /// setup-phase entity dictionary.
    ///
    /// There are no entity subclasses (no <c>Card</c>, <c>Zone</c>,
    /// <c>Player</c> in C#) — entity "kind" is a tag (<c>Tags.IsCard</c>,
    /// <c>Tags.IsZone</c>, …). Typed accessors are extension methods over
    /// IEntity / Entity (see <c>EntityExtensions</c> in the sample).
    /// </summary>
    public interface IEntity
    {
        int Id { get; }
        T Get<T>(TagKey<T> key);
        bool Has<T>(TagKey<T> key);

        // --- Pattern B reads — write happens through Entity ---------------

        bool Has<TTag>() where TTag : class, ITagKey<bool>;
        bool Get<TTag>() where TTag : class, ITagKey<bool>;
    }
}
