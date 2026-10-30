using Framework.Bootstrap;
using Framework.Entities;
using NUnit.Framework;

namespace Framework.Testing.Acceptance
{
    public sealed class DealDamageSubtractsFromHealth
    {
        [Test]
        [Picture("deal-damage-subtracts-from-health.md")]
        public void Dealing_Damage_Subtracts_From_A_Units_Health()
        {
            using var match = MatchHarness.Start(builder =>
            {
                builder.RegisterCommandHandler<SetHealthHandler>();
                builder.RegisterCommandHandler<DealDamageHandler>();
                builder.RegisterCommandValidator<DealDamageValidator>();
            });

            var unit = match.Engine.Entities.Create(new Card());
            Assert.IsTrue(match.Execute(new SetHealth { EntityId = unit.Id, Value = 30 }).Accepted);

            var result = match.Execute(new DealDamage { EntityId = unit.Id, Amount = 12 });

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(18, match.Engine.Tags.Get(unit.Id, SampleTags.Health));
        }
    }
}
