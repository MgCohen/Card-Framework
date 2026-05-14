#nullable enable
using VerticalSlice.Engine.Commands;
using VerticalSlice.Engine.Entities;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Moves a card to a new zone — one tag write on the card's
    /// <c>Tags.Zone</c> reference. "Card is in zone X" is just
    /// <c>card.ZoneId() == X.Id</c>; no separate per-zone collection to
    /// keep in sync, so the move is a single primitive write.
    ///
    /// Hooks can attach to MoveCard the same way they attach to DealDamage
    /// (e.g., "before a card moves to graveyard, fire its deathrattle").
    /// </summary>
    public sealed class MoveCard : Command
    {
        public IEntity Card { get; set; }
        public IEntity Destination { get; set; }

        public MoveCard(IEntity card, IEntity destination)
        {
            Card = card;
            Destination = destination;
        }

        public override void Apply(ICommandScope scope)
        {
            var card = scope.Resolve(Card);
            var dest = scope.Resolve(Destination);
            if (card == null || dest == null) return;
            card.SetZone(dest);
        }
    }
}
