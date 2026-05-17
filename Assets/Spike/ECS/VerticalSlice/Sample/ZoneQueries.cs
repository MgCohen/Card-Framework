#nullable enable
using System.Collections.Generic;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Pipeline;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Convenience queries that walk the entity store via tag predicates.
    /// The "structure" (zones contain cards) lives entirely in tags —
    /// these helpers project the structure as <see cref="IEnumerable{T}"/>
    /// for authors and UI.
    ///
    /// <para>Allocates a small <c>List</c> per call (via
    /// <see cref="EntityQuery.ToList"/>). Hot-path callers should iterate
    /// <c>store.With(...)</c> directly to stay zero-alloc; these helpers
    /// exist for UI / setup / test ergonomics.</para>
    /// </summary>
    public static class ZoneQueries
    {
        /// <summary>All cards currently placed in <paramref name="zone"/>.</summary>
        public static IEnumerable<IEntity> CardsIn(this EntityStore store, IEntity zone)
            => store.With(Tags.IsCard).And(Tags.Zone, zone.Id).ToList();

        /// <summary>All zones in the world.</summary>
        public static IEnumerable<IEntity> Zones(this EntityStore store)
            => store.With(Tags.IsZone).ToList();

        /// <summary>All cards in the world.</summary>
        public static IEnumerable<IEntity> Cards(this EntityStore store)
            => store.With(Tags.IsCard).ToList();

        /// <summary>All players in the world.</summary>
        public static IEnumerable<IEntity> Players(this EntityStore store)
            => store.With(Tags.IsPlayer).ToList();

        /// <summary>All cards owned by <paramref name="player"/>.</summary>
        public static IEnumerable<IEntity> CardsOwnedBy(this EntityStore store, IEntity player)
            => store.With(Tags.IsCard).And(Tags.Owner, player.Id).ToList();

        /// <summary>All cards <i>not</i> owned by <paramref name="player"/>
        /// (the "enemy cards" view, useful for AOE effects).</summary>
        public static IEnumerable<IEntity> CardsNotOwnedBy(this EntityStore store, IEntity player)
        {
            var list = new List<IEntity>();
            foreach (var e in store.With(Tags.IsCard))
            {
                var owner = e.OwnerId();
                if (owner != 0 && owner != player.Id) list.Add(e);
            }
            return list;
        }
    }
}
