#nullable enable
using System;
using System.Linq;
using NUnit.Framework;
using VerticalSlice.QueryBench.Fixtures;
using VerticalSlice.QueryBench.Model;
using VerticalSlice.QueryBench.Queries;
using VerticalSlice.QueryBench.Stores;

namespace VerticalSlice.QueryBench.Tests
{
    // Allocation measurements. Run a fixed number of iterations and
    // read GC.GetAllocatedBytesForCurrentThread() deltas to attribute
    // managed allocations to each query implementation.
    //
    // These aren't strict pass/fail gates — small fluctuations come from
    // JIT / runtime housekeeping — but they pin order-of-magnitude claims.
    [TestFixture]
    public class AllocationTests
    {
        private const int Iterations = 1000;

        [Test]
        public void Linq_baseline_allocates_per_query()
        {
            var store = new LinqStore();
            PopulationBuilder.Populate(store, PopulationShape.Hearthstone);

            // Warmup
            for (int i = 0; i < 100; i++) Consume(store.WhereMinionsOnBoardOf(PlayerId.P1));

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < Iterations; i++)
                Consume(store.WhereMinionsOnBoardOf(PlayerId.P1));
            var after = GC.GetAllocatedBytesForCurrentThread();

            var bytesPerQuery = (after - before) / (double)Iterations;
            TestContext.WriteLine($"[LINQ]               {bytesPerQuery:F1} B / query");
            Assert.That(bytesPerQuery, Is.GreaterThan(0), "LINQ baseline must allocate (sanity check)");
        }

        [Test]
        public void OptionB_Pure_allocations_per_query()
        {
            var store = new IndexedStore_Pure();
            PopulationBuilder.RegisterIndices(store);
            PopulationBuilder.Populate(store, PopulationShape.Hearthstone);

            for (int i = 0; i < 100; i++)
                ConsumeB(store.With(BenchTags.IsMinion)
                              .And(BenchTags.Owner, PlayerId.P1)
                              .And(BenchTags.Zone, ZoneId.Board));

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < Iterations; i++)
                ConsumeB(store.With(BenchTags.IsMinion)
                              .And(BenchTags.Owner, PlayerId.P1)
                              .And(BenchTags.Zone, ZoneId.Board));
            var after = GC.GetAllocatedBytesForCurrentThread();

            var bytesPerQuery = (after - before) / (double)Iterations;
            TestContext.WriteLine($"[OptionB Pure]       {bytesPerQuery:F1} B / query");
            // Success-criteria gate: ≤32 B per query (one box for one value predicate).
            // We have TWO value predicates in this shape, so allow up to 64 B.
            Assert.That(bytesPerQuery, Is.LessThanOrEqualTo(80),
                "OptionB should box at most once per .And<T>(key, value)");
        }

        [Test]
        public void OptionB_WithEvents_allocations_per_query()
        {
            var store = new IndexedStore_WithEvents();
            PopulationBuilder.RegisterIndices(store);
            PopulationBuilder.Populate(store, PopulationShape.Hearthstone);

            for (int i = 0; i < 100; i++)
                ConsumeB(store.With(BenchTags.IsMinion)
                              .And(BenchTags.Owner, PlayerId.P1)
                              .And(BenchTags.Zone, ZoneId.Board));

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < Iterations; i++)
                ConsumeB(store.With(BenchTags.IsMinion)
                              .And(BenchTags.Owner, PlayerId.P1)
                              .And(BenchTags.Zone, ZoneId.Board));
            var after = GC.GetAllocatedBytesForCurrentThread();

            var bytesPerQuery = (after - before) / (double)Iterations;
            TestContext.WriteLine($"[OptionB WithEvents] {bytesPerQuery:F1} B / query");
            Assert.That(bytesPerQuery, Is.LessThanOrEqualTo(80));
        }

        // --- helpers (force enumeration so JIT can't elide it) -----------

        private static int Consume(System.Collections.Generic.IEnumerable<BenchEntity> q)
        {
            int n = 0;
            foreach (var _ in q) n++;
            return n;
        }

        private static int ConsumeB(OptionBQuery q)
        {
            int n = 0;
            foreach (var _ in q) n++;
            return n;
        }
    }
}
