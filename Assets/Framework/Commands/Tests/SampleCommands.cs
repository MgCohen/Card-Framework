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

    public sealed class Boom : Command { }

    public sealed class BoomException : System.Exception { }

    public sealed class BoomHandler : CommandHandler<Boom>
    {
        public override void Handle(Boom cmd) => throw new BoomException();
    }
}
