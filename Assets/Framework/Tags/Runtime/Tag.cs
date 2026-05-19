namespace Framework.Tags
{
    /// <summary>
    /// Generic-static accessor for Pattern B tags — base (untyped) form. Returns the
    /// <see cref="TagKey"/> base for slot identity. Use <see cref="Tag{TClass, T}"/>
    /// when the typed key is needed for Set/Get without a cast.
    /// </summary>
    public static class Tag<TClass> where TClass : ITagKey
    {
        public static readonly TagKey Key = TagRegistry.For<TClass>();
    }

    /// <summary>
    /// Generic-static accessor for Pattern B tags — typed form. Returns
    /// <see cref="TagKey{T}"/> directly so call sites avoid casting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Resolves to the same singleton as <see cref="Tag{TClass}.Key"/>; the second
    /// type parameter <typeparamref name="T"/> just propagates the value type
    /// declared on <typeparamref name="TClass"/>'s <see cref="ITagKey{T}"/> interface.
    /// </para>
    /// <example>
    /// <code>
    /// var key = Tag&lt;Deathrattle, bool&gt;.Key;       // TagKey&lt;bool&gt;
    /// entity.Set(key, true);
    /// </code>
    /// </example>
    /// <para>
    /// For named-field convenience (<c>Tag.Deathrattle</c>), downstream code declares
    /// its own static container per assembly — partial classes cannot span assemblies,
    /// so each game/test/module owns its own named-field surface:
    /// </para>
    /// <code>
    /// // In your game assembly:
    /// public static class Tag
    /// {
    ///     public static readonly TagKey&lt;bool&gt; Deathrattle = Tag&lt;Deathrattle, bool&gt;.Key;
    /// }
    /// </code>
    /// <para>
    /// Materialization (manual one-liner / source-gen) is per-assembly. Call sites
    /// read identically regardless of mechanism.
    /// </para>
    /// </remarks>
    public static class Tag<TClass, T> where TClass : ITagKey<T>
    {
        public static readonly TagKey<T> Key = (TagKey<T>)TagRegistry.For<TClass>();
    }
}
