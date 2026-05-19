namespace Framework.Tags
{
    public static class Tag<TClass> where TClass : ITagKey
    {
        public static readonly TagKey Key = TagRegistry.For<TClass>();
    }

    public static class Tag<TClass, T> where TClass : ITagKey<T>
    {
        public static readonly TagKey<T> Key = (TagKey<T>)TagRegistry.For<TClass>();
    }
}
