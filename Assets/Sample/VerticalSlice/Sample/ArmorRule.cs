#nullable enable
using System;
using VerticalSlice.Engine.Pipeline;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Armor reduces incoming damage by <c>Target.Armor</c>, floored at 0.
    /// Runs Before+PreEffect — strictly after <see cref="DivineShieldRule"/>
    /// (Before+Replacement). Shield gets first crack: if it absorbs the
    /// hit, Amount is already 0 and Armor's reduction is a no-op. Armor
    /// has no consumable state — just mutates cmd.Amount.
    /// </summary>
    public static class ArmorRule
    {
        public static void Register(CommandService commands)
        {
            commands.Subscribe<DealDamage>(HookPhase.Before, HookPriority.PreEffect,
                (cmd, scope) =>
                {
                    if (cmd.Amount <= 0) return;
                    var armor = cmd.Target.Armor();
                    if (armor <= 0) return;
                    cmd.Amount = Math.Max(0, cmd.Amount - armor);
                });
        }
    }
}
