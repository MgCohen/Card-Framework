#nullable enable
namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// When a hook runs relative to a command's <c>Apply</c>.
    /// Two values only — kept as an enum for compactness; this is
    /// framework-internal control flow, not a game-facing tag value.
    /// </summary>
    public enum HookPhase
    {
        Before,
        After,
    }
}
