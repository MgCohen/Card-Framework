#nullable enable
using System.Threading.Tasks;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>Mode-2 command — applies damage and brackets it with Pre/Post DamageDealt events.</summary>
    public sealed class DealDamageCommand : Command<Unit>
    {
        public int Amount;

        // Runtime-only — set programmatically by the host, not authored as a port wire.
        // CardSandbox declares Convention = AllFieldsIn so [GraphPortIgnore] excludes this field
        // from the generated editor mirror / runtime port set.
        [GraphPortIgnore]
        public object? Target;

        public override async Task<Unit> Execute(ICardEffectScope scope, Flow flow)
        {
            var evt = new DamageDealt { Amount = Amount, Target = Target };
            await scope.Bus.Publish(evt, Timing.Before);
            scope.Damage.Apply(evt.Target, evt.Amount);
            await scope.Bus.Publish(evt, Timing.After);
            return Unit.Default;
        }
    }
}
