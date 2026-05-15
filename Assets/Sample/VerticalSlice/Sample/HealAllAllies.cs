#nullable enable
using System.Collections.Generic;
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Entities;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Sample deathrattle: heals every friendly minion on a battlefield zone
    /// by 3. Skips the dying minion itself (already heading to graveyard;
    /// healing it doesn't matter). Cards in hand and the friendly hero
    /// are not affected — keeps the effect focused on the visible board.
    ///
    /// Demonstrates per-card distinct deathrattles — paired with
    /// <see cref="DamageAllEnemies"/>, two different cards can have
    /// completely different deathrattle behaviors with no per-card
    /// boilerplate beyond the attach call.
    /// </summary>
    public sealed class HealAllAllies : Effect<Deathrattle>
    {
        public const int HealAmount = 3;

        public HealAllAllies(Deathrattle trigger) : base(trigger) { }

        public override void Run(IEffectScope scope)
        {
            int srcOwner = Trigger.Source.OwnerId();
            var targets = new List<IEntity>();

            foreach (var e in scope.With(Tags.IsMinion).And(Tags.Owner, srcOwner))
            {
                if (e.Id == Trigger.Source.Id) continue;   // skip dying self
                var zone = scope.Get(e.ZoneId());
                if (zone != null && zone.IsBattlefield())
                    targets.Add(e);
            }

            foreach (var t in targets)
                scope.Execute(new Heal(Trigger.Source, t, HealAmount));
        }
    }
}
