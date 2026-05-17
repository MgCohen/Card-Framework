#nullable enable
namespace VerticalSlice.Engine.Stream
{
    /// <summary>
    /// How a block ended. Carried on <see cref="BlockEndEvent"/> so replay
    /// + tooling can distinguish blocks that ran to completion from blocks
    /// that errored out or were cancelled.
    ///
    /// The slice has no cancellation primitive yet — Cancelled is reserved
    /// for the future request/response system (where a player cancel or
    /// timeout could short-circuit a block).
    /// </summary>
    public enum BlockOutcome
    {
        Completed,
        Errored,
        Cancelled,
    }
}
