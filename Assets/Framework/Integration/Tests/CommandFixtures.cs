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
