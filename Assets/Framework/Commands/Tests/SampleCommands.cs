using Framework.Tags;

namespace Framework.Commands.Tests
{
    // Phase 0 verification surface: trivial concrete commands + handlers that prove the
    // orchestration path (intent -> handler -> one real service call -> one mutation),
    // bracketed by a Block. Lives in Tests, not Runtime — no real game commands ship yet.

    public sealed record HealthTag() : Tag<int>(9001);   // test-only slot, clear of sample tags (1001-1003)

    // The "happy path" command: carries pure data (an entity id + a value), no infra/behavior.
    public sealed class SetHealth : Command
    {
        public int EntityId;
        public int Value;
    }

    // Handler holds the logic and ctor-injects the service it orchestrates (the L4 split).
    public sealed class SetHealthHandler : CommandHandler<SetHealth>
    {
        static readonly HealthTag Health = new();
        readonly TagService _tags;

        public SetHealthHandler(TagService tags) => _tags = tags;

        public override void Handle(SetHealth cmd) => _tags.Set(cmd.EntityId, Health, cmd.Value);
    }

    // The "handler throws" command: proves Execute brackets an Errored block and rethrows.
    public sealed class Boom : Command { }

    public sealed class BoomException : System.Exception { }

    public sealed class BoomHandler : CommandHandler<Boom>
    {
        public override void Handle(Boom cmd) => throw new BoomException();
    }
}
