using Framework.Tags;

namespace Framework.Entities.Tests
{
    static class SampleTags
    {
        public static readonly TagKey<int>  Health = new(1001);
        public static readonly TagKey<int>  Attack = new(1002);
        public static readonly TagKey<bool> IsCard = new(1003);
    }

    [TagSlot(1100)] sealed class SampleDeathrattle : ITagKey<bool> { }
    [TagSlot(1101)] sealed class SamplePoison      : ITagKey<int>  { }

    internal static partial class Tag
    {
        public static readonly TagKey<bool> SampleDeathrattle = Tag<SampleDeathrattle, bool>.Key;
        public static readonly TagKey<int>  SamplePoison      = Tag<SamplePoison, int>.Key;
    }
}
