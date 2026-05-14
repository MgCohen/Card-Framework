#nullable enable
using VerticalSlice.Engine.Commands;
using VerticalSlice.Engine.Entities;

namespace VerticalSlice.Sample
{
    /// <summary>
    /// Primitive damage command. <see cref="Amount"/> is mutable so
    /// Replacement-band Before hooks (DivineShield, Armor) can zero or
    /// reduce it before <see cref="Apply"/> runs.
    ///
    /// Payload carries <see cref="IEntity"/> references — wire-stable and
    /// safe for the read-only hook surface. <see cref="Apply"/> resolves
    /// to the mutable <see cref="Entity"/> via <see cref="ICommandScope"/>
    /// to write the Hp tag.
    /// </summary>
    public sealed class DealDamage : Command
    {
        public IEntity? Source { get; set; }
        public IEntity Target { get; set; }
        public int Amount { get; set; }

        public DealDamage(IEntity? source, IEntity target, int amount)
        {
            Source = source;
            Target = target;
            Amount = amount;
        }

        public override void Apply(ICommandScope scope)
        {
            if (Amount <= 0) return;
            var target = scope.Resolve(Target);
            if (target == null) return;
            target.SetHp(target.Hp() - Amount);
        }
    }
}
