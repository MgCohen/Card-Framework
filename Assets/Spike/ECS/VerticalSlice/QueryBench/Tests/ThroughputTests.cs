#nullable enable
using System;
using System.Diagnostics;
using NUnit.Framework;
using VerticalSlice.QueryBench.Fixtures;
using VerticalSlice.QueryBench.Model;
using VerticalSlice.QueryBench.Queries;
using VerticalSlice.QueryBench.Stores;

namespace VerticalSlice.QueryBench.Tests
{
    // Wall-time throughput. Stopwatch over a fixed iteration count.
    //
    // Each test prints µs/query and the speedup ratio vs the LINQ baseline.
    // The single Gate test enforces the spike's success criterion: at the
    // Hearthstone-sized / mid-selectivity / 2+-predicate config, the
    // indexed shape must be at least 5× faster than LINQ.
    [TestFixture]
    [Category("Benchmark")]
    public class ThroughputTests
    {
        private const int Iterations = 10_000;
        private const int Warmup     = 1_000;

        [TestCase(PopulationShape.Hearthstone)]
        [TestCase(PopulationShape.BigBoard)]
        [TestCase(PopulationShape.Stress)]
        public void Compound_minions_on_board_of_p1(PopulationShape shape)
        {
            var linq    = new LinqStore();
            var pure    = new IndexedStore_Pure();
            var events  = new IndexedStore_WithEvents();
            PopulationBuilder.RegisterIndices(pure);
            PopulationBuilder.RegisterIndices(events);
            PopulationBuilder.Populate(linq, shape);
            PopulationBuilder.Populate(pure, shape);
            PopulationBuilder.Populate(events, shape);

            double tLinq = TimeLinq(linq);
            double tPureB = TimeOptionB(pure);
            double tEventsB = TimeOptionB(events);

            TestContext.WriteLine($"=== {shape} — minions on board of P1 ===");
            TestContext.WriteLine($"  LINQ               {tLinq * 1e6 / Iterations,8:F2} µs/q");
            TestContext.WriteLine($"  OptionB Pure       {tPureB   * 1e6 / Iterations,8:F2} µs/q   ({tLinq / tPureB:F1}× LINQ)");
            TestContext.WriteLine($"  OptionB WithEvents {tEventsB * 1e6 / Iterations,8:F2} µs/q   ({tLinq / tEventsB:F1}× LINQ)");
        }

        [Test]
        public void Gate_hearthstone_compound_must_beat_linq_by_5x()
        {
            var linq   = new LinqStore();
            var events = new IndexedStore_WithEvents();
            PopulationBuilder.RegisterIndices(events);
            PopulationBuilder.Populate(linq, PopulationShape.Hearthstone);
            PopulationBuilder.Populate(events, PopulationShape.Hearthstone);

            double tLinq    = TimeLinq(linq);
            double tIndexed = TimeOptionB(events);

            double speedup = tLinq / tIndexed;
            TestContext.WriteLine($"Hearthstone compound speedup (IsMinion-driven): {speedup:F2}×");
            Assert.That(speedup, Is.GreaterThanOrEqualTo(5.0),
                $"Indexed compound must be ≥5× faster than LINQ at Hearthstone scale. Got {speedup:F2}×.");
        }

        // Critical comparison: what if we drive off the most-selective index
        // (Zone=Board) instead of the marker? Reveals whether driver-set
        // selection is the load-bearing optimization.
        [TestCase(PopulationShape.Hearthstone)]
        [TestCase(PopulationShape.BigBoard)]
        [TestCase(PopulationShape.Stress)]
        public void Selective_driver_compound(PopulationShape shape)
        {
            var linq   = new LinqStore();
            var events = new IndexedStore_WithEvents();
            PopulationBuilder.RegisterIndices(events);
            PopulationBuilder.Populate(linq, shape);
            PopulationBuilder.Populate(events, shape);

            // Warmup
            for (int i = 0; i < Warmup; i++)
                CountB(events.Where(BenchTags.Zone, ZoneId.Board).And(BenchTags.IsMinion).And(BenchTags.Owner, PlayerId.P1));
            for (int i = 0; i < Warmup; i++)
                CountLinq(linq.WhereMinionsOnBoardOf(PlayerId.P1));

            var sw = Stopwatch.StartNew();
            int sink = 0;
            for (int i = 0; i < Iterations; i++)
                sink += CountLinq(linq.WhereMinionsOnBoardOf(PlayerId.P1));
            sw.Stop();
            double tLinq = sw.Elapsed.TotalSeconds;

            sw.Restart();
            for (int i = 0; i < Iterations; i++)
                sink += CountB(events.Where(BenchTags.Zone, ZoneId.Board).And(BenchTags.IsMinion).And(BenchTags.Owner, PlayerId.P1));
            sw.Stop();
            double tIdx = sw.Elapsed.TotalSeconds;

            GC.KeepAlive(sink);
            TestContext.WriteLine($"=== {shape} — Zone=Board-driven compound ===");
            TestContext.WriteLine($"  LINQ              {tLinq*1e6/Iterations:F2} µs/q");
            TestContext.WriteLine($"  OptionB Selective {tIdx*1e6/Iterations:F2} µs/q   ({tLinq/tIdx:F1}× LINQ)");
        }

        [Test]
        public void Marker_only_query_speedup()
        {
            var linq   = new LinqStore();
            var events = new IndexedStore_WithEvents();
            PopulationBuilder.RegisterIndices(events);
            PopulationBuilder.Populate(linq, PopulationShape.Hearthstone);
            PopulationBuilder.Populate(events, PopulationShape.Hearthstone);

            // Warmup
            for (int i = 0; i < Warmup; i++) { CountLinq(linq.Where(BenchTags.IsMinion)); }
            for (int i = 0; i < Warmup; i++) { CountB(events.With(BenchTags.IsMinion)); }

            var sw = Stopwatch.StartNew();
            int sink = 0;
            for (int i = 0; i < Iterations; i++)
                sink += CountLinq(linq.Where(BenchTags.IsMinion));
            sw.Stop();
            double tLinq = sw.Elapsed.TotalSeconds;

            sw.Restart();
            for (int i = 0; i < Iterations; i++)
                sink += CountB(events.With(BenchTags.IsMinion));
            sw.Stop();
            double tIdx = sw.Elapsed.TotalSeconds;

            TestContext.WriteLine($"Marker-only — LINQ {tLinq*1e6/Iterations:F2} µs/q vs Indexed {tIdx*1e6/Iterations:F2} µs/q ({tLinq/tIdx:F1}×)");
            TestContext.WriteLine($"(sink={sink} — prevents JIT elision)");
        }

        [Test]
        public void Mutation_throughput_pure_vs_with_events()
        {
            // Measures the cost of building the population: event-dispatch +
            // log-append delta between Pure and WithEvents.
            // Pure builds first to warm the JIT for both impls.
            var sw = new Stopwatch();
            const int N = 200;

            // Warmup
            for (int w = 0; w < 5; w++)
            {
                var p = new IndexedStore_Pure();    PopulationBuilder.RegisterIndices(p);
                PopulationBuilder.Populate(p, PopulationShape.Hearthstone);
                var ev = new IndexedStore_WithEvents(); PopulationBuilder.RegisterIndices(ev);
                PopulationBuilder.Populate(ev, PopulationShape.Hearthstone);
            }

            sw.Restart();
            for (int i = 0; i < N; i++)
            {
                var p = new IndexedStore_Pure();
                PopulationBuilder.RegisterIndices(p);
                PopulationBuilder.Populate(p, PopulationShape.Hearthstone);
            }
            sw.Stop();
            double tPure = sw.Elapsed.TotalSeconds / N;

            sw.Restart();
            for (int i = 0; i < N; i++)
            {
                var ev = new IndexedStore_WithEvents();
                PopulationBuilder.RegisterIndices(ev);
                PopulationBuilder.Populate(ev, PopulationShape.Hearthstone);
            }
            sw.Stop();
            double tEvents = sw.Elapsed.TotalSeconds / N;

            TestContext.WriteLine($"Population-build (Hearthstone, ~200 entities):");
            TestContext.WriteLine($"  Pure       {tPure   * 1e6:F1} µs / population");
            TestContext.WriteLine($"  WithEvents {tEvents * 1e6:F1} µs / population");
            TestContext.WriteLine($"  Delta (event dispatch + log append): {(tEvents - tPure) * 1e6:F1} µs / population, {(tEvents/tPure - 1) * 100:F1}%");
        }

        // --- timing helpers ---------------------------------------------

        private static double TimeLinq(LinqStore store)
        {
            for (int i = 0; i < Warmup; i++) CountLinq(store.WhereMinionsOnBoardOf(PlayerId.P1));
            var sw = Stopwatch.StartNew();
            int sink = 0;
            for (int i = 0; i < Iterations; i++)
                sink += CountLinq(store.WhereMinionsOnBoardOf(PlayerId.P1));
            sw.Stop();
            GC.KeepAlive(sink);
            return sw.Elapsed.TotalSeconds;
        }

        private static double TimeOptionB<TStore>(TStore store) where TStore : class
        {
            // Dispatch via concrete type to skip interface overhead in the hot loop.
            if (store is IndexedStore_Pure p)
            {
                for (int i = 0; i < Warmup; i++)
                    CountB(p.With(BenchTags.IsMinion).And(BenchTags.Owner, PlayerId.P1).And(BenchTags.Zone, ZoneId.Board));
                var sw = Stopwatch.StartNew();
                int sink = 0;
                for (int i = 0; i < Iterations; i++)
                    sink += CountB(p.With(BenchTags.IsMinion).And(BenchTags.Owner, PlayerId.P1).And(BenchTags.Zone, ZoneId.Board));
                sw.Stop();
                GC.KeepAlive(sink);
                return sw.Elapsed.TotalSeconds;
            }
            if (store is IndexedStore_WithEvents ev)
            {
                for (int i = 0; i < Warmup; i++)
                    CountB(ev.With(BenchTags.IsMinion).And(BenchTags.Owner, PlayerId.P1).And(BenchTags.Zone, ZoneId.Board));
                var sw = Stopwatch.StartNew();
                int sink = 0;
                for (int i = 0; i < Iterations; i++)
                    sink += CountB(ev.With(BenchTags.IsMinion).And(BenchTags.Owner, PlayerId.P1).And(BenchTags.Zone, ZoneId.Board));
                sw.Stop();
                GC.KeepAlive(sink);
                return sw.Elapsed.TotalSeconds;
            }
            throw new InvalidOperationException();
        }

        private static int CountLinq(System.Collections.Generic.IEnumerable<BenchEntity> q)
        {
            int n = 0; foreach (var _ in q) n++; return n;
        }

        private static int CountB(OptionBQuery q)
        {
            int n = 0; foreach (var _ in q) n++; return n;
        }
    }
}
