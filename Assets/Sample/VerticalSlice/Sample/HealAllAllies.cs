#nullable enable
using System.Linq;
using VerticalSlice.Engine.Effects;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Sample deathrattle: heals every friendly card by 3. Friendly =
    /// same owner as the dying card. Skips the dying card itself (it's
    /// going to the graveyard; healing it doesn't matter).
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
            var allies = scope.Where(Tags.IsCard)
                              .Where(c => c.OwnerId() == srcOwner && c.Id != Trigger.Source.Id)
                              .ToList();
            foreach (var ally in allies)
                scope.Execute(new Heal(Trigger.Source, ally, HealAmount));
        }
    }
}
