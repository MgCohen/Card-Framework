#nullable enable
using System.Threading.Tasks;
using NUnit.Framework;
using Scaffold.GraphFlow;
using Scaffold.GraphFlow.CardSandbox.Cards;

namespace Scaffold.GraphFlow.CardSandbox.Tests
{
    public sealed class Strike500Tests
    {
        [Test]
        public async Task Strike500_Solo_Deals_5_Damage()
        {
            var bus = new EventBus();
            var sink = new DamageSink();
            var builder = new CardEffectBuilder(bus, sink);

            var runner = builder.Build(Strike500.BuildAsset());
            await runner.Run(new OnPlay());

            Assert.AreEqual(5, sink.LastAmount);
        }

        [Test]
        public async Task Strike500_Plus_PlusOneDamage_Trigger_Deals_6_Damage()
        {
            var bus = new EventBus();
            var sink = new DamageSink();
            var builder = new CardEffectBuilder(bus, sink);

            var s500 = builder.Build(Strike500.BuildAsset());
            var p1d  = builder.Build(PlusOneDamage.BuildAsset());

            SubscribeDamageDealtTriggers(s500, bus);
            SubscribeDamageDealtTriggers(p1d, bus);

            await s500.Run(new OnPlay());

            Assert.AreEqual(6, sink.LastAmount);
        }

        static void SubscribeDamageDealtTriggers(GraphRunner card, EventBus bus)
        {
            foreach (var entry in card.EntriesByPayload.Values)
            {
                if (entry is OnTrigger<DamageDealt> trig)
                    bus.Subscribe<DamageDealt>(async e => await card.Run(e), trig.Timing);
            }
        }
    }
}
