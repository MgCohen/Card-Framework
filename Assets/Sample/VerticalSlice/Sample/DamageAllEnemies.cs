#nullable enable
using System.Linq;
using VerticalSlice.Engine.Effects;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Sample deathrattle: deals 2 damage to every enemy card. Built by a
    /// factory at fire time, with the <see cref="Deathrattle"/> trigger
    /// payload bound on the constructor (via <see cref="Effect{T}"/>).
    ///
    /// Attached per-card via <c>cardEffects.Set&lt;Deathrattle&gt;(card,
    /// t => new DamageAllEnemies(t))</c> in setup.
    /// </summary>
    public sealed class DamageAllEnemies : Effect<Deathrattle>
    {
        public const int Damage = 2;

        public DamageAllEnemies(Deathrattle trigger) : base(trigger) { }

        public override void Run(IEffectScope scope)
        {
            int srcOwner = Trigger.Source.OwnerId();
            var enemies = scope.Where(Tags.IsCard)
                               .Where(c => c.OwnerId() != 0 && c.OwnerId() != srcOwner)
                               .ToList();
            foreach (var enemy in enemies)
                scope.Execute(new DealDamage(Trigger.Source, enemy, Damage));
        }
    }
}
