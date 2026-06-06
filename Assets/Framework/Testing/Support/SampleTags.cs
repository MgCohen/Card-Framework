using Framework.Tags;

namespace Framework.Testing
{
    // Canonical sample tag keys shared by every global test tier (Acceptance + Integration).
    // Records over Tag<T>, exposed as static aliases — the standard ergonomic. Slots are
    // arbitrary; Set/Get index the store by slot directly. Public so cross-assembly tests
    // (Support is its own assembly) can read them through the shared surface.
    public sealed record HealthTag() : Tag<int>(2001);
    public sealed record CostTag()   : Tag<int>(2002);
    public sealed record IsCardTag() : Tag<bool>(2003);

    public static class SampleTags
    {
        public static readonly Tag<int>  Health = new HealthTag();
        public static readonly Tag<int>  Cost   = new CostTag();
        public static readonly Tag<bool> IsCard = new IsCardTag();
    }
}
