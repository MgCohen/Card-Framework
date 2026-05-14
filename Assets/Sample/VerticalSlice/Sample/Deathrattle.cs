#nullable enable
using System.Linq;
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Entities;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Sample deathrattle effect: when a card with this dies (moves to a
    /// graveyard), it deals 2 damage to every enemy card. "Enemy" =
    /// <see cref="Tags.Owner"/> tag != source's owner.
    ///
    /// Demonstrates an effect that <i>fans out</i> — its body issues
    /// multiple primitive commands in sequence. Each is its own block on
    /// the wire, all nested inside the <c>Deathrattle</c> block, all of
    /// which sit inside the <c>MoveCard</c> block that triggered them.
    /// The full causal chain is recoverable from the log without any
    /// game-side annotations.
    /// </summary>
    public sealed class Deathrattle : Effect
    {
        public const int DamagePerEnemy = 2;

        public IEntity Source { get; }

        public Deathrattle(IEntity source) { Source = source; }

        public override void Run(IEffectScope scope)
        {
            int sourceOwner = Source.OwnerId();
            // "Enemy" cards = anything tagged IsCard with Owner != source's owner
            // and Owner != 0 (skip unowned).
            var enemies = scope.Where(Tags.IsCard)
                               .Where(c => c.OwnerId() != 0 && c.OwnerId() != sourceOwner)
                               .ToList();
            foreach (var enemy in enemies)
                scope.Execute(new DealDamage(Source, enemy, DamagePerEnemy));
        }
    }
}
