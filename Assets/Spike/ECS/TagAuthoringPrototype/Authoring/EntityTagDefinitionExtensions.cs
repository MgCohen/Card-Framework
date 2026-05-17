#nullable enable
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Tags;

namespace TagAuthoringPrototype.Authoring
{
    /// <summary>
    /// Ergonomic call-site overloads so <c>entity.Set(myTag, 30)</c> reads
    /// identically whether <c>myTag</c> is a code-defined
    /// <c>TagKey&lt;int&gt;</c> or a data-defined <see cref="TagDefinition{T}"/>.
    ///
    /// <para>The implicit conversion <c>TagDefinition&lt;T&gt; → TagKey&lt;T&gt;</c>
    /// is in place too, but C# generic type inference doesn't always pick
    /// it up cleanly. These extensions sidestep that — generic <c>T</c> is
    /// inferred directly from the <see cref="TagDefinition{T}"/> argument,
    /// no implicit conversion needed.</para>
    /// </summary>
    public static class EntityTagDefinitionExtensions
    {
        public static T   Get<T>   (this Entity e, TagDefinition<T> def) => e.Get   (def.Key);
        public static bool Has<T>  (this Entity e, TagDefinition<T> def) => e.Has   (def.Key);
        public static void Set<T>  (this Entity e, TagDefinition<T> def, T value) => e.Set(def.Key, value);
        public static void Remove<T>(this Entity e, TagDefinition<T> def) => e.Remove(def.Key);
    }
}
