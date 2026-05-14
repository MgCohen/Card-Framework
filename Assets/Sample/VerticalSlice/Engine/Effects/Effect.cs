#nullable enable
namespace VerticalSlice.Engine.Effects
{
    /// <summary>
    /// Base for every effect — a Run that does the work plus a Validate
    /// that gates whether it runs at all. Validate is a separate step (not
    /// an early-return inside Run) so the engine can answer "did this
    /// effect actually run" later, and so cost-paying / option-showing
    /// systems can hook between the two without changing Run.
    ///
    /// Authors do not manage scope open/close — <c>EffectService.Run</c>
    /// wraps <see cref="Run"/> in a Block on the wire automatically, and
    /// short-circuits the entire dispatch (no block emitted) if
    /// <see cref="Validate"/> returns false.
    /// </summary>
    public abstract class Effect
    {
        public virtual bool Validate(IEffectScope scope) => true;
        public abstract void Run(IEffectScope scope);
    }

    /// <summary>
    /// Effect carrying a typed payload, bound at construction. Used for
    /// effects that react to something specific — a command (hook effect)
    /// or a typed trigger record (direct effect).
    ///
    /// Examples:
    /// <list type="bullet">
    /// <item><c>DivineShieldRule : Effect&lt;DealDamage&gt;</c> — hook
    /// effect bound to the in-flight DealDamage at fire time by the
    /// factory passed to <c>EffectService.Register</c>.</item>
    /// <item><c>DamageAllEnemies : Effect&lt;Deathrattle&gt;</c> — direct
    /// effect bound to a <c>Deathrattle</c> payload when the card's
    /// direct trigger fires via <c>IEffectScope.FireDirect</c>.</item>
    /// </list>
    /// The payload is on the constructor, not as a parameter on Validate /
    /// Run — keeps the per-effect API stable across all effect kinds.
    /// </summary>
    public abstract class Effect<TPayload> : Effect
    {
        public TPayload Trigger { get; }
        protected Effect(TPayload trigger) { Trigger = trigger; }
    }
}
