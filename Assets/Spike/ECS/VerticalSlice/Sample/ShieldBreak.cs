#nullable enable
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Entities;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Effect fired by the DivineShield rule when it absorbs damage. Wraps
    /// the consume-self <see cref="BreakDivineShield"/> command in its own
    /// Block on the wire — so the log records "Shield broke (because of …)"
    /// as a distinct effect block, not a stray tag mutation.
    /// </summary>
    public sealed class ShieldBreak : Effect
    {
        public IEntity Target { get; }

        public ShieldBreak(IEntity target) { Target = target; }

        public override void Run(IEffectScope scope)
        {
            scope.Execute(new BreakDivineShield(Target));
        }
    }
}
