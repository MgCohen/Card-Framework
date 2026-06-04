using Framework.Commands;
using Framework.Tags;

namespace Framework.Integration.Tests
{
    // Game-side stub commands + handlers used to drive the integration spines. Registered through
    // DI in the (b) flow and hand-wired in the (a) harness — the same way wire handlers are.
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

    public sealed class SetHealthValidator : CommandValidator<SetHealth>
    {
        public override bool Validate(SetHealth cmd, out string reason)
        {
            if (cmd.Value < 0) { reason = "NegativeHealth"; return false; }
            reason = null;
            return true;
        }
    }

    // Phase 2: read-modify-write command whose validator reads world state (ctor-injects
    // TagService). Drives the stateful-validation shape through both integration spines.
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
