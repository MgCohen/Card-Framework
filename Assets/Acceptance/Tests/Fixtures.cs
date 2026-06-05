using Framework.Commands;
using Framework.Tags;

namespace Acceptance
{
    public sealed record HealthTag() : Tag<int>(3001);

    static class SampleTags
    {
        public static readonly Tag<int> Health = new HealthTag();
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
}
