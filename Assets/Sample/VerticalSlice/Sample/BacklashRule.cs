#nullable enable
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Pipeline;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Backlash as an <see cref="Effect{DealDamage}"/>. After+Reaction
    /// hook on DealDamage. When a card with <c>Tags.Backlash</c> tag
    /// deals damage that actually landed, runs a <see cref="Backlash"/>
    /// effect that retaliates 1 back from target to source. Per-source
    /// tag gating prevents recursion (the retaliation's source is the
    /// original target, who typically doesn't have the tag).
    ///
    /// Backlash stays tag-gated (state property) rather than direct-trigger
    /// here — the user's mental model: state tags drive on/off behaviors,
    /// direct triggers drive distinct per-card effects. Backlash is "this
    /// card has a passive retaliation" — uniform behavior, opt-in by tag.
    /// </summary>
    public sealed class BacklashRule : Effect<DealDamage>
    {
        public BacklashRule(DealDamage cmd) : base(cmd) { }

        public override bool Validate(IEffectScope scope)
        {
            if (Trigger.Source == null) return false;
            if (!Trigger.Source.Backlash()) return false;
            if (Trigger.Amount <= 0) return false;
            if (Trigger.Source.Id == Trigger.Target.Id) return false;
            return true;
        }

        public override void Run(IEffectScope scope)
            => scope.Run(new Backlash(Trigger.Target, Trigger.Source!));

        public static void Register(EffectStore store)
            => store.Subscribe<DealDamage>(
                new HookConfig
                {
                    Phase = HookPhase.After,
                    Priority = HookPriority.Reaction,
                    Source = nameof(BacklashRule),
                },
                cmd => new BacklashRule(cmd));
    }
}
