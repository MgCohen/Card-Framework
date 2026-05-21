using Framework.Tags;

namespace Framework.Entities.Tests
{
    static class SampleTags
    {
        public static readonly TagKey<int>  Health = new(1001);
        public static readonly TagKey<int>  Attack = new(1002);
        public static readonly TagKey<bool> IsCard = new(1003);
    }
}
