namespace Framework.Tags.Tests
{
    /// <summary>
    /// Test-local named-field container for Pattern B sample tags. Lives in the Tests
    /// namespace because the sample keyword classes (<see cref="SampleDeathrattle"/>,
    /// <see cref="SamplePoison"/>) do — partial classes can't span assemblies, so each
    /// assembly owns its own <c>Tag</c> container alongside its keyword class declarations.
    /// </summary>
    /// <remarks>
    /// One line per keyword. Same pattern downstream game assemblies will follow when
    /// they declare their own keyword tags. Source-gen could later replace these
    /// hand-written lines with no call-site changes.
    /// </remarks>
    internal static class Tag
    {
        public static readonly TagKey<bool> SampleDeathrattle = Tag<SampleDeathrattle, bool>.Key;
        public static readonly TagKey<int>  SamplePoison      = Tag<SamplePoison, int>.Key;
    }
}
