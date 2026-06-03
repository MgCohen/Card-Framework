using Framework.Commands;
using Framework.Tags;

namespace Framework.Integration.Tests
{
    // A game-side command + its handler, registered through DI exactly like the wire handlers.
    public sealed class SetHealthCommand : Command
    {
        public int EntityId;
        public int Value;
    }

    sealed class SetHealthCommandHandler : CommandHandler<SetHealthCommand>
    {
        readonly TagService _tags;
        public SetHealthCommandHandler(TagService tags) { _tags = tags; }
        public override void Handle(SetHealthCommand cmd) => _tags.Set(cmd.EntityId, SampleTags.Health, cmd.Value);
    }
}
