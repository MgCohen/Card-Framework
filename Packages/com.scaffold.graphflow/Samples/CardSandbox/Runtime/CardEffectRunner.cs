#nullable enable
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    public sealed class CardEffectRunner : GraphRunner, ICardEffectScope
    {
        public EventBus Bus { get; }
        public DamageSink Damage { get; }

        public CardEffectRunner(BakedGraph baked, EventBus bus, DamageSink damage) : base(baked)
        {
            Bus = bus;
            Damage = damage;
        }
    }

    public sealed class CardEffectBuilder : GraphBuilder<CardEffectRunner>
    {
        readonly EventBus _bus;
        readonly DamageSink _damage;

        public CardEffectBuilder(EventBus bus, DamageSink damage)
        {
            _bus = bus;
            _damage = damage;
        }

        protected override CardEffectRunner CreateRunner(BakedGraph baked) =>
            new(baked, _bus, _damage);
    }
}
