using System.Linq;
using Framework.Bootstrap;
using Framework.Entities;
using Framework.Tags;
using Framework.Wire;
using NUnit.Framework;

namespace Framework.Testing.Acceptance
{
    public sealed class OneCommandAppliesSeveralMutations
    {
        [Test]
        [Picture("one-command-applies-several-mutations.md")]
        public void One_Command_Applies_Several_Mutations_Under_A_Single_Block()
        {
            using var match = MatchHarness.Start(builder =>
                builder.RegisterCommandHandler<IntroduceCardHandler>());

            var card = match.Engine.Entities.Create(new Card());
            var before = match.Engine.Stream.Count;

            var result = match.Execute(new IntroduceCard { EntityId = card.Id, Cost = 3 });

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(3, match.Engine.Tags.Get(card.Id, SampleTags.Cost));
            Assert.IsTrue(match.Engine.Tags.Get(card.Id, SampleTags.IsCard));

            var emitted = Enumerable.Range(before, match.Engine.Stream.Count - before)
                                    .Select(i => match.Engine.Stream[i]).ToList();
            var start = emitted.First() as BlockStartEvent;
            var end   = emitted.Last() as BlockEndEvent;
            Assert.IsNotNull(start, "the command opens a block first");
            Assert.AreEqual((byte)BlockKind.Command, start.Kind);
            Assert.IsNotNull(end, "the command closes the block last");
            Assert.AreEqual(start.Id, end.Id);
            Assert.AreEqual((byte)BlockOutcome.Completed, end.Outcome);
            Assert.AreEqual(1, emitted.OfType<BlockStartEvent>().Count(), "one Command block brackets both mutations");

            var inside = emitted.Skip(1).Take(emitted.Count - 2).ToList();
            Assert.AreEqual(1, inside.OfType<TagSetEvent<int>>().Count(), "the int mutation lands inside the block");
            Assert.AreEqual(1, inside.OfType<TagSetEvent<bool>>().Count(), "the bool mutation lands inside the block");
        }
    }
}
