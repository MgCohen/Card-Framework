namespace Framework.Tags
{
    public static class Tag<TClass> where TClass : ITagKey
    {
        public static readonly TagKey Key = TagRegistry.For<TClass>();
    }
}
