#nullable enable
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>Damage event — published by DealDamageCommand once with Timing.Before, once with Timing.After.</summary>
    [GraphEvent]
    public sealed class DamageDealt
    {
        public int Amount;
        public object? Target;
    }
}
