using Framework.Bootstrap;
using Framework.Entities;
using Framework.Wire;
using NUnit.Framework;

namespace Framework.Testing.Acceptance
{
    public sealed class IllegalCommandEmitsNothing
    {
        [Test]
        [Picture("illegal-command-emits-nothing.md")]
        public void A_Rejected_Command_Changes_Nothing_And_Emits_Nothing()
        {
            using var match = MatchHarness.Start(builder =>
            {
                builder.RegisterCommandHandler<DealDamageHandler>();
                builder.RegisterCommandValidator<DealDamageValidator>();
            });

            // A unit with no health recorded — not a damageable target.
            var unit = match.Engine.Entities.Create(new Card());

            var emitted = 0;
            void Count(WireEvent _) => emitted++;
            match.Engine.Stream.OnAppended += Count;
            var result = match.Execute(new DealDamage { EntityId = unit.Id, Amount = 5 });
            match.Engine.Stream.OnAppended -= Count;

            Assert.IsFalse(result.Accepted);
            Assert.AreEqual("NotDamageable", result.RejectReason);
            Assert.AreEqual(0, emitted, "a rejected command puts nothing on the wire");
            Assert.IsFalse(match.Engine.Tags.Has(unit.Id, SampleTags.Health),
                "the rejected command left world state untouched");
        }
    }
}
