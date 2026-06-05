using Framework.Commands;
using Framework.Tags;

namespace Acceptance
{
    public sealed record HealthTag() : Tag<int>(3001);

    public sealed record CostTag() : Tag<int>(3002);
    public sealed record IsCardTag() : Tag<bool>(3003);

    static class SampleTags
    {
        public static readonly Tag<int> Health = new HealthTag();
        public static readonly Tag<int> Cost = new CostTag();
        public static readonly Tag<bool> IsCard = new IsCardTag();
    }

    public sealed class SetHealth : Command
    {
        public int EntityId;
        public int Value;
    }

    sealed class SetHealthHandler : CommandHandler<SetHealth>
    {
        readonly TagService _tags;
        public SetHealthHandler(TagService tags) { _tags = tags; }
        public override void Handle(SetHealth cmd) => _tags.Set(cmd.EntityId, SampleTags.Health, cmd.Value);
    }

    // Scenario 2: read-modify-write whose validator reads live world state (ctor-injects
    // TagService). The handler subtracts from the current value; the validator gates on it.
    public sealed class DealDamage : Command
    {
        public int EntityId;
        public int Amount;
    }

    sealed class DealDamageHandler : CommandHandler<DealDamage>
    {
        readonly TagService _tags;
        public DealDamageHandler(TagService tags) { _tags = tags; }
        public override void Handle(DealDamage cmd)
            => _tags.Set(cmd.EntityId, SampleTags.Health,
                         _tags.Get(cmd.EntityId, SampleTags.Health) - cmd.Amount);
    }

    sealed class DealDamageValidator : CommandValidator<DealDamage>
    {
        readonly TagService _tags;
        public DealDamageValidator(TagService tags) { _tags = tags; }
        public override bool Validate(DealDamage cmd, out string reason)
        {
            if (cmd.Amount <= 0) { reason = "NonPositiveDamage"; return false; }
            if (!_tags.Has(cmd.EntityId, SampleTags.Health)) { reason = "NotDamageable"; return false; }
            reason = null;
            return true;
        }
    }

    // Scenario 3: a single command whose handler performs several mutations. The framework
    // brackets them under one Command block; both tags land from one Execute.
    public sealed class IntroduceCard : Command
    {
        public int EntityId;
        public int Cost;
    }

    sealed class IntroduceCardHandler : CommandHandler<IntroduceCard>
    {
        readonly TagService _tags;
        public IntroduceCardHandler(TagService tags) { _tags = tags; }
        public override void Handle(IntroduceCard cmd)
        {
            _tags.Set(cmd.EntityId, SampleTags.Cost, cmd.Cost);
            _tags.Set(cmd.EntityId, SampleTags.IsCard, true);
        }
    }
}
