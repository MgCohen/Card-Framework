using Framework.Tags;

namespace Framework.Entities
{
    public static class EntityExtensions
    {
        public static void Set<TTag, T>(this Entity entity, T value)
            where TTag : ITagKey<T> where T : unmanaged
            => entity.Set(Tag<TTag, T>.Key, value);

        public static T Get<TTag, T>(this Entity entity)
            where TTag : ITagKey<T> where T : unmanaged
            => entity.Get(Tag<TTag, T>.Key);

        public static bool Has<TTag>(this Entity entity) where TTag : ITagKey
            => entity.Has(Tag<TTag>.Key);

        public static bool Remove<TTag>(this Entity entity) where TTag : ITagKey
            => entity.Remove(Tag<TTag>.Key);
    }
}
