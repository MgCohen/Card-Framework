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
    /// SBAs run AFTER every top-level command/effect tree completes
    /// (engine drain pass). The engine collects every SBA's emissions,
    /// dispatches each as a re-entrant <see cref="CommandService.Execute"/>
    /// (so its own inline subtree — hooks, sub-commands, attached effects —
    /// runs normally and nests cleanly under the SBA-emitted block on the
    /// wire), then re-evaluates. Loops until every SBA is silent — death
    /// cascades, deathrattle chains, win conditions all converge here
    /// without bespoke orchestration.
    ///
    /// Contract: SBAs are read-only against the scope. They MUST NOT call
    /// <c>scope.Execute</c> / <c>scope.Run</c> / <c>scope.FireDirect</c> /
    /// <c>scope.OpenRequest</c>. Yield commands; let the engine dispatch
    /// them. The full <see cref="IEffectScope"/> surface is here for
    /// convenience (same Get/All/Where the effects see) — treat the
    /// mutating methods as off-limits.
    /// </summary>
    public interface ISBA
    {
        IEnumerable<Command> Evaluate(IEffectScope scope);
    }
}
