#nullable enable
using System;
using VerticalSlice.QueryBench.Model;
using VerticalSlice.QueryBench.Stores;

namespace VerticalSlice.QueryBench.Fixtures
{
    public enum PopulationShape
    {
        // ~200 entities. Hearthstone-shape:
        //   * 2 heroes
        //   * ~16 minions on board (8 per side, mid-game)
        //   * ~20 in hands
        //   * ~80 in decks
        //   * ~80 in graveyards
        Hearthstone,

        // ~500 entities. Bigger boards, deeper graveyards.
        BigBoard,

        // ~5000 entities. Not a real game — scaling test.
        Stress,
    }

    public static class PopulationBuilder
    {
        // Fills the store with a deterministic population so every
        // implementation sees the same entities and tag distribution.
        public static void Populate(IBenchStore store, PopulationShape shape, int seed = 1)
        {
            var rng = new Random(seed);
            (int heroes, int onBoard, int inHand, int inDeck, int inGrave) counts = shape switch
            {
                PopulationShape.Hearthstone => (2,  16,  20,  80,  80),
                PopulationShape.BigBoard    => (2,  40,  30, 200, 230),
                PopulationShape.Stress      => (2, 200, 100,2400,2300),
                _ => throw new ArgumentOutOfRangeException(nameof(shape))
            };

            // Heroes
            for (int i = 0; i < counts.heroes; i++)
            {
                var owner = i == 0 ? PlayerId.P1 : PlayerId.P2;
                var e = store.Create();
                store.Set(e, BenchTags.IsHero, true);
                store.Set(e, BenchTags.Owner, owner);
                store.Set(e, BenchTags.Health, 30);
            }

            // Minions by zone
            FillMinions(store, counts.onBoard,  ZoneId.Board,     rng);
            FillMinions(store, counts.inHand,   ZoneId.Hand,      rng);
            FillMinions(store, counts.inDeck,   ZoneId.Deck,      rng);
            FillMinions(store, counts.inGrave,  ZoneId.Graveyard, rng);
        }

        private static void FillMinions(IBenchStore store, int n, ZoneId zone, Random rng)
        {
            for (int i = 0; i < n; i++)
            {
                var owner = (i % 2 == 0) ? PlayerId.P1 : PlayerId.P2;
                var e = store.Create();
                store.Set(e, BenchTags.IsCard, true);
                store.Set(e, BenchTags.IsMinion, true);
                store.Set(e, BenchTags.Owner, owner);
                store.Set(e, BenchTags.Zone, zone);
                store.Set(e, BenchTags.Attack, rng.Next(1, 10));
                store.Set(e, BenchTags.Health, rng.Next(1, 10));
            }
        }

        // Helper to declare the indices the bench cares about, once per store.
        public static void RegisterIndices(IndexedStore_Pure store)
        {
            store.IndexMarker(BenchTags.IsCard);
            store.IndexMarker(BenchTags.IsMinion);
            store.IndexMarker(BenchTags.IsHero);
            store.IndexBy(BenchTags.Zone);
            store.IndexBy(BenchTags.Owner);
        }

        public static void RegisterIndices(IndexedStore_WithEvents store)
        {
            store.IndexMarker(BenchTags.IsCard);
            store.IndexMarker(BenchTags.IsMinion);
            store.IndexMarker(BenchTags.IsHero);
            store.IndexBy(BenchTags.Zone);
            store.IndexBy(BenchTags.Owner);
        }
    }
}
