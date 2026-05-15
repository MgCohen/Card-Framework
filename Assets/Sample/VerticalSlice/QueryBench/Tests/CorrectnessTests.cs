#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VerticalSlice.QueryBench.Fixtures;
using VerticalSlice.QueryBench.Model;
using VerticalSlice.QueryBench.Queries;
using VerticalSlice.QueryBench.Stores;

namespace VerticalSlice.QueryBench.Tests
{
    // Every implementation must return the same id set for the same logical
    // query. Without this, throughput numbers are meaningless.
    [TestFixture]
    public class CorrectnessTests
    {
        [TestCase(PopulationShape.Hearthstone)]
        [TestCase(PopulationShape.BigBoard)]
        [TestCase(PopulationShape.Stress)]
        public void All_implementations_agree_on_minions_on_board_of_p1(PopulationShape shape)
        {
            var linq    = new LinqStore();
            var pure    = new IndexedStore_Pure();
            var events  = new IndexedStore_WithEvents();
            PopulationBuilder.RegisterIndices(pure);
            PopulationBuilder.RegisterIndices(events);
            PopulationBuilder.Populate(linq, shape);
            PopulationBuilder.Populate(pure, shape);
            PopulationBuilder.Populate(events, shape);

            var fromLinq = linq.WhereMinionsOnBoardOf(PlayerId.P1)
                               .Select(e => e.Id).OrderBy(x => x).ToArray();

            var fromOptionB_Pure = ToArray(pure.With(BenchTags.IsMinion)
                                              .And(BenchTags.Owner, PlayerId.P1)
                                              .And(BenchTags.Zone, ZoneId.Board));

            var fromOptionB_Events = ToArray(events.With(BenchTags.IsMinion)
                                                  .And(BenchTags.Owner, PlayerId.P1)
                                                  .And(BenchTags.Zone, ZoneId.Board));

            Assert.That(fromOptionB_Pure,   Is.EqualTo(fromLinq), "OptionB_Pure disagrees with LINQ");
            Assert.That(fromOptionB_Events, Is.EqualTo(fromLinq), "OptionB_Events disagrees with LINQ");
        }

        [TestCase(PopulationShape.Hearthstone)]
        [TestCase(PopulationShape.BigBoard)]
        public void Marker_only_query_agrees(PopulationShape shape)
        {
            var linq   = new LinqStore();
            var events = new IndexedStore_WithEvents();
            PopulationBuilder.RegisterIndices(events);
            PopulationBuilder.Populate(linq, shape);
            PopulationBuilder.Populate(events, shape);

            var fromLinq = linq.Where(BenchTags.IsMinion).Select(e => e.Id).OrderBy(x => x).ToArray();
            var fromIdx  = ToArray(events.With(BenchTags.IsMinion));
            Assert.That(fromIdx, Is.EqualTo(fromLinq));
        }

        [TestCase(PopulationShape.Hearthstone)]
        public void Single_value_query_agrees(PopulationShape shape)
        {
            var linq   = new LinqStore();
            var events = new IndexedStore_WithEvents();
            PopulationBuilder.RegisterIndices(events);
            PopulationBuilder.Populate(linq, shape);
            PopulationBuilder.Populate(events, shape);

            var fromLinq = linq.Entities.Values
                .Where(e => e.Has(BenchTags.Zone) && e.Get(BenchTags.Zone) == ZoneId.Hand)
                .Select(e => e.Id).OrderBy(x => x).ToArray();
            var fromIdx = ToArray(events.Where(BenchTags.Zone, ZoneId.Hand));
            Assert.That(fromIdx, Is.EqualTo(fromLinq));
        }

        private static int[] ToArray(OptionBQuery q)
        {
            var ids = new List<int>();
            foreach (var e in q) ids.Add(e.Id);
            ids.Sort();
            return ids.ToArray();
        }
    }
}
