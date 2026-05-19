#nullable enable
using System.Threading.Tasks;
using Scaffold.GraphFlow;

namespace Scaffold.GraphFlow.CardSandbox
{
    public readonly struct Unit { public static readonly Unit Default = default; }

    public abstract class Command<TResult>
    {
        public abstract Task<TResult> Execute(ICardEffectScope scope, Flow flow);
    }
}
