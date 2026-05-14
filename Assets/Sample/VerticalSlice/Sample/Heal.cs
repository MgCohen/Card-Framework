#nullable enable
using System;
using VerticalSlice.Engine.Commands;
using VerticalSlice.Engine.Entities;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Primitive heal command — dual of <see cref="DealDamage"/>.
    /// Mutable <see cref="Amount"/> so future Replacement-band hooks could
    /// buff/nerf incoming heals. Apply clamps to <c>Tags.MaxHealth</c>;
    /// healing past the cap is equality-skipped by <c>Entity.Set</c> (no
    /// spurious TagChange).
    /// </summary>
    public sealed class Heal : Command
    {
        public IEntity? Source { get; set; }
        public IEntity Target { get; set; }
        public int Amount { get; set; }

        public Heal(IEntity? source, IEntity target, int amount)
        {
            Source = source;
            Target = target;
            Amount = amount;
        }

        public override void Apply(ICommandScope scope)
        {
            if (Amount <= 0) return;
            var target = scope.Resolve(Target);
            if (target == null) return;
            var newHealth = Math.Min(target.MaxHealth(), target.Health() + Amount);
            target.SetHealth(newHealth);
        }
    }
}
