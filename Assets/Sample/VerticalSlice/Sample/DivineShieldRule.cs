#nullable enable
using VerticalSlice.Engine.Pipeline;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Divine Shield. Before+Replacement on <see cref="DealDamage"/>: if the
    /// target has Shield, zero the incoming Amount and fire a
    /// <see cref="ShieldBreak"/> effect that flips the shield off. The Hp
    /// write becomes a no-op via the equality-skip guard in <c>Entity.Set</c>.
    ///
    /// The hook does two things:
    /// <list type="bullet">
    /// <item>Mutates <c>cmd.Amount</c> directly (allowed — cmd is passed by
    /// reference; mutating in-flight payload is the Replacement-band
    /// pattern).</item>
    /// <item>Runs <see cref="ShieldBreak"/> via <c>scope.Run</c> — the
    /// "consume self" state change rides through Effect → Command and
    /// records a clean Block in the wire instead of a stray tag write.</item>
    /// </list>
    /// </summary>
    public static class DivineShieldRule
    {
        public static void Register(CommandService commands)
        {
            commands.Subscribe<DealDamage>(HookPhase.Before, HookPriority.Replacement,
                (cmd, scope) =>
                {
                    if (cmd.Amount <= 0) return;
                    if (!cmd.Target.DivineShield()) return;

                    cmd.Amount = 0;
                    scope.Run(new ShieldBreak(cmd.Target));
                });
        }
    }
}
