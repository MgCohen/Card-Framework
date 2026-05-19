namespace Framework.Tags.Tests
{
    static class SampleTags
    {
        public static readonly TagKey<int>  Health = new(1001);
        public static readonly TagKey<int>  Attack = new(1002);
        public static readonly TagKey<bool> IsCard = new(1003);
    }

    [TagSlot(1100)]
    sealed class SampleDeathrattle : ITagKey<bool>
    {
        public int SourceEntityId { get; }
        public SampleDeathrattle(int sourceEntityId) { SourceEntityId = sourceEntityId; }
    }

    [TagSlot(1101)]
    sealed class SamplePoison : ITagKey<int>
    {
        public int Source { get; }
        public int Amount { get; }
        public SamplePoison(int source, int amount) { Source = source; Amount = amount; }
    }

    internal static class Tag
    {
        public static readonly TagKey<bool> SampleDeathrattle = Tag<SampleDeathrattle, bool>.Key;
        public static readonly TagKey<int>  SamplePoison      = Tag<SamplePoison, int>.Key;
    }
}
