#nullable enable
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Pipeline;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Divine Shield as an <see cref="Effect{DealDamage}"/>. Subscribed
    /// at Before+Replacement via <c>EffectService.Register</c>:
    /// <code>
    /// effects.Register&lt;DealDamage&gt;(HookPhase.Before, HookPriority.Replacement,
    ///     cmd =&gt; new DivineShieldRule(cmd));
    /// </code>
    /// Validate gates the firing entirely — no wire block emitted if the
    /// target has no shield or amount is already zero. When it does run,
    /// it zeroes Amount (so the Hp write equality-skips) and fires a
    /// <see cref="ShieldBreak"/> effect that consumes the shield via a
    /// proper command.
    /// </summary>
    public sealed class DivineShieldRule : Effect<DealDamage>
    {
        public DivineShieldRule(DealDamage cmd) : base(cmd) { }

        public override bool Validate(IEffectScope scope)
            => Trigger.Amount > 0 && Trigger.Target.DivineShield();

        public override void Run(IEffectScope scope)
        {
            Trigger.Amount = 0;
            scope.Run(new ShieldBreak(Trigger.Target));
        }

        public static void Register(EffectService effects)
            => effects.Register<DealDamage>(
                HookPhase.Before, HookPriority.Replacement,
                cmd => new DivineShieldRule(cmd));
    }
}
