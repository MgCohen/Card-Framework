#nullable enable
using VerticalSlice.Engine.Pipeline;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Wires the <see cref="Backlash"/> effect to the After+Reaction phase
    /// of <see cref="DealDamage"/>. When a card with <c>Tags.Backlash</c>
    /// deals damage, the hook fires the Backlash effect — which opens its
    /// own Block, issues a retaliation DealDamage, closes.
    ///
    /// Per-source tag gating prevents recursion: the retaliation's source
    /// is the original target, who typically doesn't have Backlash, so the
    /// hook's guard short-circuits on the second pass.
    /// </summary>
    public static class BacklashRule
    {
        public static void Register(CommandService commands)
        {
            commands.Subscribe<DealDamage>(HookPhase.After, HookPriority.Reaction,
                (cmd, scope) =>
                {
                    if (cmd.Source == null) return;
                    if (!cmd.Source.Backlash()) return;
                    if (cmd.Amount <= 0) return;                  // damage was zeroed
                    if (cmd.Source.Id == cmd.Target.Id) return;   // never reflect onto self

                    scope.Run(new Backlash(cmd.Target, cmd.Source));
                });
        }
    }
}
