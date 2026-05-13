#nullable enable
using VerticalSlice.Engine.Commands;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Pipeline;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Semantic damage command. <c>Amount</c> is mutable so
    /// Replacement-band hooks can zero or modify it before Apply runs.
    /// </summary>
    public sealed class DealDamage : Command
    {
        public Entity? Source { get; set; }
        public Entity Target { get; set; }
        public int Amount { get; set; }

        public DealDamage(Entity? source, Entity target, int amount)
        {
            Source = source;
            Target = target;
            Amount = amount;
        }

        public override BlockType BlockType => BlockType.Power;

        public override void Apply(World world)
        {
            if (Amount <= 0) return;
            var currentHp = Target.Get(Tags.Hp);
            Target.Set(Tags.Hp, currentHp - Amount);
        }
    }
}
