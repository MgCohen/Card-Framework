using Framework.Tags;

namespace Framework.Entities.Tests
{
    // Sample game-side tag keys as records over Tag<T>. Exposed as static aliases so call
    // sites read `SampleTags.Health` (the standard's "game-side static alias" ergonomic).
    public sealed record HealthTag() : Tag<int>(1001);
    public sealed record AttackTag() : Tag<int>(1002);
    public sealed record IsCardTag() : Tag<bool>(1003);

    static class SampleTags
    {
        public static readonly Tag<int>  Health = new HealthTag();
        public static readonly Tag<int>  Attack = new AttackTag();
        public static readonly Tag<bool> IsCard = new IsCardTag();
    }
}
