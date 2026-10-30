using Framework.Tags;

namespace Framework.Commands.Tests
{
    // Phase 0 fixtures: a happy-path command that calls one real service, and one that throws.
    public sealed record HealthTag() : Tag<int>(9001);   // test-only slot, clear of sample tags (1001-1003)

    public sealed class SetHealth : Command
    {
        public int EntityId;
        public int Value;
    }

    public sealed class SetHealthHandler : CommandHandler<SetHealth>
    {
        static readonly HealthTag Health = new();
        readonly TagService _tags;

        public SetHealthHandler(TagService tags) => _tags = tags;

        public override void Handle(SetHealth cmd) => _tags.Set(cmd.EntityId, Health, cmd.Value);
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

    // Phase 2 fixture: a read-modify-write command whose validator reads world state.
    // SetHealth blind-sets; DealDamage reads the current value first — and its validator
    // ctor-injects TagService to reject targets that have no Health tag at all. This is the
    // shape Phase 1's payload-only SetHealthValidator never exercised: a validator with a
    // service dependency, deciding on live state rather than the command's own fields.
    public sealed class DealDamage : Command
    {
        public int EntityId;
        public int Amount;
    }

    public sealed class DealDamageHandler : CommandHandler<DealDamage>
    {
        static readonly HealthTag Health = new();
        readonly TagService _tags;

        public DealDamageHandler(TagService tags) => _tags = tags;

        public override void Handle(DealDamage cmd)
            => _tags.Set(cmd.EntityId, Health, _tags.Get(cmd.EntityId, Health) - cmd.Amount);
    }

    public sealed class DealDamageValidator : CommandValidator<DealDamage>
    {
        static readonly HealthTag Health = new();
        readonly TagService _tags;

        public DealDamageValidator(TagService tags) => _tags = tags;

        public override bool Validate(DealDamage cmd, out string reason)
        {
            if (cmd.Amount <= 0) { reason = "NonPositiveDamage"; return false; }        // payload
            if (!_tags.Has(cmd.EntityId, Health)) { reason = "NotDamageable"; return false; } // world state
            reason = null;
            return true;
        }
    }

    public sealed class Boom : Command { }

    public sealed class BoomException : System.Exception { }

    public sealed class BoomHandler : CommandHandler<Boom>
    {
        public override void Handle(Boom cmd) => throw new BoomException();
    }
}
