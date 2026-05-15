#nullable enable
using System.Collections.Generic;
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Entities;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Sample deathrattle: deals 2 damage to every enemy character "in play"
    /// — the enemy hero plus every enemy minion sitting on a battlefield
    /// zone. Cards in hand or graveyard are excluded (the AOE shouldn't
    /// touch unplayed spells or already-dead minions).
    ///
    /// Attached per-card via <c>EffectStore.SetDirect&lt;Deathrattle&gt;(card,
    /// t => new DamageAllEnemies(t))</c> in setup.
    /// </summary>
    public sealed class DamageAllEnemies : Effect<Deathrattle>
    {
        public const int Damage = 2;

        public DamageAllEnemies(Deathrattle trigger) : base(trigger) { }

        public override void Run(IEffectScope scope)
        {
            int srcOwner = Trigger.Source.OwnerId();
            var targets = new List<IEntity>();

            // Enemy hero — any player entity that isn't the source's owner.
            foreach (var e in scope.With(Tags.IsPlayer))
            {
                if (e.Id == srcOwner) continue;
                targets.Add(e);
            }

            // Enemy minions on a battlefield zone.
            foreach (var e in scope.With(Tags.IsMinion))
            {
                int owner = e.OwnerId();
                if (owner == 0 || owner == srcOwner) continue;
                var zone = scope.Get(e.ZoneId());
                if (zone != null && zone.IsBattlefield())
                    targets.Add(e);
            }

            foreach (var t in targets)
                scope.Execute(new DealDamage(Trigger.Source, t, Damage));
        }
    }
}
