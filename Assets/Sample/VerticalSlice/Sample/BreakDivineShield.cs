#nullable enable
using VerticalSlice.Engine.Commands;
using VerticalSlice.Engine.Entities;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Primitive command for flipping the DivineShield tag off. Lives as
    /// its own command so the Shield rule's "consume self" write rides the
    /// command pipeline (block + hooks + log) instead of bypassing it via
    /// a direct entity.Set call.
    /// </summary>
    public sealed class BreakDivineShield : Command
    {
        public IEntity Target { get; }

        public BreakDivineShield(IEntity target) { Target = target; }

        public override void Apply(ICommandScope scope)
        {
            scope.Resolve(Target)?.SetDivineShield(false);
        }
    }
}
