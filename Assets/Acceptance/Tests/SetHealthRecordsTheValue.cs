using Framework.Bootstrap;
using Framework.Entities;
using NUnit.Framework;

namespace Acceptance
{
    public sealed class SetHealthRecordsTheValue
    {
        [Test]
        [Picture("set-health-records-the-value.md")]
        public void Setting_A_Units_Health_Records_The_Value()
        {
            using var match = MatchHarness.Start(builder =>
                builder.RegisterCommandHandler<SetHealthHandler>());

            var unit = match.Engine.Entities.Create(new Card());

            var result = match.Execute(new SetHealth { EntityId = unit.Id, Value = 20 });

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(20, match.Engine.Tags.Get(unit.Id, SampleTags.Health));
        }
    }
}
