#nullable enable
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Entities;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Healing Potion — sibling of <see cref="Fireball"/>. Issues a single
    /// <see cref="Heal"/> primitive command. Bus dispatch is by exact
    /// command type, so the DivineShield + Armor hooks (registered for
    /// <see cref="DealDamage"/>) do not fire on this command path.
    /// </summary>
    public sealed class HealingPotion : Effect
    {
        public const int Amount = 4;

        public IEntity? Caster { get; }
        public IEntity Target { get; }

        public HealingPotion(IEntity? caster, IEntity target)
        {
            Caster = caster;
            Target = target;
        }

        public override void Run(IEffectScope scope)
        {
            scope.Execute(new Heal(Caster, Target, Amount));
        }
    }
}
