using Framework.Wire;

namespace Framework.Commands
{
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
