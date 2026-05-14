#nullable enable
namespace VerticalSlice.Engine.Stream
{
    /// <summary>
    /// Categorical label on a block, set by the engine based on which
    /// dispatch path opened it. Distinct from the block's <i>name</i>
    /// (which is the runnable's type name).
    ///
    /// Lives on the wire as metadata; tooling routes on this without
    /// having to parse names. Authors do not declare this — the dispatch
    /// site (EffectService.Run / CommandService.Execute) sets it.
    ///
    /// Add new kinds when new dispatch surfaces appear (e.g., a future
    /// TriggerService or RequestService).
    /// </summary>
    public enum BlockKind
    {
        Effect,    // EffectService.Run opened this
        Command,   // CommandService.Execute opened this
        Request,   // RequestsService.Open opened this (options block)
    }
}
