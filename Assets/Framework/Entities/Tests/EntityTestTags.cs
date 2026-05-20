using Framework.Tags;

namespace Framework.Entities.Tests
{
    [TagSlot(9100)] sealed class DeathrattleTestTag : ITagKey<bool> { }
    [TagSlot(9101)] sealed class PoisonTestTag      : ITagKey<int>  { }

    internal static partial class Tag
    {
        public static readonly TagKey<bool> Deathrattle = Tag<DeathrattleTestTag, bool>.Key;
        public static readonly TagKey<int>  Poison      = Tag<PoisonTestTag, int>.Key;
    }
}
