#nullable enable
using System.Collections.Generic;
using VerticalSlice.Engine.Commands;
using VerticalSlice.Engine.Effects;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// State-Based Action — a pure function over <see cref="World"/> that
    /// emits commands to bring the world back into a coherent state after
    /// a player action resolves. The classic example is "any minion at
    /// Health ≤ 0 moves to its owner's graveyard"; the SBA scans, finds
    /// the dying minions, and yields the right <c>MoveCard</c> commands.
    ///
    /// <para><b>MTG-shaped ordering</b> (CR 117.5). The engine's Evaluate
    /// runs a fixpoint: every registered SBA evaluates, each emission
    /// dispatches inline through <see cref="CommandService.Execute"/>
    /// (so its hooks fire and any After-phase reactions push effects onto
    /// the stack as usual). The fixpoint repeats until a pass emits no
    /// commands. Only then does the engine resolve one stack frame; the
    /// SBA fixpoint then re-runs against the new state, and so on. SBAs
    /// themselves carry no priority — they all just happen, simultaneously
    /// in spirit, before any stack resolution.</para>
    ///
    /// <para><b>Contract.</b> SBAs are read-only against the scope. They
    /// MUST NOT call <c>scope.Execute</c> / <c>scope.Run</c> /
    /// <c>scope.FireDirect</c> / <c>scope.OpenRequest</c>. Yield commands;
    /// let the engine dispatch them. The full <see cref="IEffectScope"/>
    /// surface is here for convenience (same Get/All/Where the effects
    /// see) — treat the mutating methods as off-limits.</para>
    /// </summary>
    public interface ISBA
    {
        IEnumerable<Command> Evaluate(IEffectScope scope);
    }
}
