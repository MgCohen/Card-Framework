#nullable enable
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Entities;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Healing Touch — sibling of <see cref="Fireball"/>. Issues a single
    /// <see cref="Heal"/> primitive command. Bus dispatch is by exact
    /// command type, so the DivineShield + Armor hooks (registered for
    /// <see cref="DealDamage"/>) do not fire on this command path.
    /// </summary>
    public sealed class HealingTouch : Effect
    {
        public const int Amount = 8;

        public IEntity? Source { get; }
        public IEntity Target { get; }

        public HealingTouch(IEntity? source, IEntity target)
        {
            Source = source;
            Target = target;
        }

        public override void Run(IEffectScope scope)
        {
            scope.Execute(new Heal(Source, Target, Amount));
        }
    }
}
