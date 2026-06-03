using Framework.Wire;

namespace Framework.Commands
{
    // Server-only intake: brackets a command's mutations with a Block (L2) and runs its
    // handler inline + synchronously (L3). The handler calls the existing Tags/Entities/States
    // services; THEIR mutation events are what cross the wire — the command never serializes
    // (L1). Validation enters in Phase 1; for now every command is accepted.
    public sealed class CommandService
    {
        readonly CommandHandlerRegistry _handlers;
        readonly BlockScope _blocks;

        public CommandService(CommandHandlerRegistry handlers, BlockScope blocks)
        {
            _handlers = handlers;
            _blocks   = blocks;
        }

        public CommandResult Execute(Command cmd)
        {
            var id = _blocks.Open(BlockKind.Command);
            try
            {
                _handlers.HandlerFor(cmd.GetType()).Handle(cmd);
            }
            catch
            {
                _blocks.Close(id, BlockOutcome.Errored);
                throw;
            }
            _blocks.Close(id, BlockOutcome.Completed);
            return CommandResult.Accept();
        }
    }
}
