using Framework.Commands;
using Framework.Tags;

namespace Framework.Integration.Tests
{
    // Game-side stub commands + handlers, registered through DI like the wire handlers, used to
    // drive mutations in the composed end-to-end flow. Two distinct types prove registry dispatch.
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

    public sealed class MarkCard : Command
    {
        public int EntityId;
    }

    sealed class MarkCardHandler : CommandHandler<MarkCard>
    {
        readonly TagService _tags;
        public MarkCardHandler(TagService tags) { _tags = tags; }
        public override void Handle(MarkCard cmd) => _tags.Set(cmd.EntityId, SampleTags.IsCard, true);
    }
}
