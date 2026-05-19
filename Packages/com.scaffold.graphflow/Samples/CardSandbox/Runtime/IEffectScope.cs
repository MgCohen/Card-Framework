#nullable enable
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    public interface ICardEffectScope
    {
        EventBus Bus { get; }
        DamageSink Damage { get; }
    }

    public sealed class DamageSink
    {
        public int LastAmount { get; private set; }
        public object? LastTarget { get; private set; }

        public void Apply(object? target, int amount)
        {
            LastTarget = target;
            LastAmount = amount;
        }
    }
}
