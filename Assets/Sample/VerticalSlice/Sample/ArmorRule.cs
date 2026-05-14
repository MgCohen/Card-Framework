#nullable enable
using System;
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Pipeline;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Armor as an <see cref="Effect{DealDamage}"/>. Runs at
    /// Before+PreEffect — strictly after <see cref="DivineShieldRule"/>
    /// (Before+Replacement). If shield absorbed the hit, Amount is
    /// already 0 and Validate short-circuits here. Otherwise reduces
    /// Amount by the target's armor, floored at 0. Armor is permanent
    /// (no consume-self).
    /// </summary>
    public sealed class ArmorRule : Effect<DealDamage>
    {
        public ArmorRule(DealDamage cmd) : base(cmd) { }

        public override bool Validate(IEffectScope scope)
            => Trigger.Amount > 0 && Trigger.Target.Armor() > 0;

        public override void Run(IEffectScope scope)
            => Trigger.Amount = Math.Max(0, Trigger.Amount - Trigger.Target.Armor());

        public static void Register(EffectService effects)
            => effects.Register<DealDamage>(
                HookPhase.Before, HookPriority.PreEffect,
                cmd => new ArmorRule(cmd));
    }
}
