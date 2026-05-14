#nullable enable
using System.Collections.Generic;
using System.Linq;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Pipeline;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Convenience queries that walk the flat entity store via tag
    /// predicates. The "structure" (zones contain cards) lives entirely in
    /// tags — these helpers project the structure as IEnumerables for
    /// authors and UI.
    /// </summary>
    public static class ZoneQueries
    {
        /// <summary>All cards currently placed in <paramref name="zone"/>.</summary>
        public static IEnumerable<Entity> CardsIn(this World world, IEntity zone)
            => world.All().Where(e => e.IsCard() && e.ZoneId() == zone.Id);

        /// <summary>All zones in the world.</summary>
        public static IEnumerable<Entity> Zones(this World world)
            => world.Where(Tags.IsZone);

        /// <summary>All cards in the world.</summary>
        public static IEnumerable<Entity> Cards(this World world)
            => world.Where(Tags.IsCard);
    }
}
