using Framework.Bootstrap;
using Framework.Entities;
using NUnit.Framework;

namespace Acceptance
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

            var result = match.Execute(new IntroduceCard { EntityId = card.Id, Cost = 3 });

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(3, match.Engine.Tags.Get(card.Id, SampleTags.Cost));
            Assert.IsTrue(match.Engine.Tags.Get(card.Id, SampleTags.IsCard));
        }
    }
}
