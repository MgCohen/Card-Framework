using Framework.Commands;
using Framework.Tags;

namespace Framework.Testing
{
    // Sample commands + their handlers/validators, shared across the global test tiers. Registered
    // through DI (RegisterCommandHandler/Validator) in the bootstrap-wired flows and hand-wired in
    // the EngineStack spine. Public so cross-assembly tests can register/drive them through Support.

    // Scenario 1: a plain write — the handler records a value the validator only range-checks.
    public sealed class SetHealth : Command
    {
        public int EntityId;
        public int Value;
    }

    public sealed class SetHealthHandler : CommandHandler<SetHealth>
    {
        readonly TagService _tags;
        public SetHealthHandler(TagService tags) { _tags = tags; }
        public override void Handle(SetHealth cmd) => _tags.Set(cmd.EntityId, SampleTags.Health, cmd.Value);
    }

    public sealed class SetHealthValidator : CommandValidator<SetHealth>
    {
        public override bool Validate(SetHealth cmd, out string reason)
        {
            if (cmd.Value < 0) { reason = "NegativeHealth"; return false; }
            reason = null;
            return true;
        }
    }

    // Scenario 2: read-modify-write whose validator reads live world state (ctor-injects
    // TagService). The handler subtracts from the current value; the validator gates on it.
    public sealed class DealDamage : Command
    {
        public int EntityId;
        public int Amount;
    }

    public sealed class DealDamageHandler : CommandHandler<DealDamage>
    {
        readonly TagService _tags;
        public DealDamageHandler(TagService tags) { _tags = tags; }
        public override void Handle(DealDamage cmd)
            => _tags.Set(cmd.EntityId, SampleTags.Health,
                         _tags.Get(cmd.EntityId, SampleTags.Health) - cmd.Amount);
    }

    public sealed class DealDamageValidator : CommandValidator<DealDamage>
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

    public sealed class IntroduceCardHandler : CommandHandler<IntroduceCard>
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
