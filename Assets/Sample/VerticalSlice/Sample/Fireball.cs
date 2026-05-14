#nullable enable
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Entities;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// The slice's sample effect. Fired via
    /// <c>engine.Effects.Run(new Fireball(caster, target))</c>; the
    /// <c>EffectService</c> opens a Block named "Fireball" with
    /// {Caster, Target} payload, then runs the body — which issues a
    /// single <see cref="DealDamage"/> command. That command opens its own
    /// nested block; hooks (Shield, Armor, Backlash) wire in there.
    /// </summary>
    public sealed class Fireball : Effect
    {
        public const int Damage = 6;

        public IEntity? Caster { get; }
        public IEntity Target { get; }

        public Fireball(IEntity? caster, IEntity target)
        {
            Caster = caster;
            Target = target;
        }

        public override void Run(IEffectScope scope)
        {
            scope.Execute(new DealDamage(Caster, Target, Damage));
        }
    }
}
