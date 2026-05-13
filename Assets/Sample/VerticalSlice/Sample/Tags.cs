#nullable enable
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// All tags the slice uses. Wire keys are explicit strings (no
    /// implicit derivation from C# names) so renaming the C# field does
    /// not break the wire format.
    /// </summary>
    public static class Tags
    {
        public static readonly TagKey<int>  Hp           = new("hp");
        public static readonly TagKey<int>  MaxHp        = new("maxHp");
        public static readonly TagKey<int>  Attack       = new("attack");
        public static readonly TagKey<bool> DivineShield = new("divineShield");
    }
}
