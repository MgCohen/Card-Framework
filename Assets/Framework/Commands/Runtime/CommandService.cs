using Framework.Wire;

namespace Framework.Commands
{
    public sealed class CommandService
    {
        readonly CommandHandlerRegistry _handlers;
        readonly CommandValidatorRegistry _validators;
        readonly BlockScope _blocks;

        public CommandService(
            CommandHandlerRegistry handlers,
            CommandValidatorRegistry validators,
            BlockScope blocks)
        {
            _handlers   = handlers;
            _validators = validators;
            _blocks     = blocks;
        }

        public CommandResult Execute(Command cmd)
        {
            // Validate before opening the block: a rejected command emits nothing.
            if (_validators.TryFor(cmd.GetType(), out var validator)
                && !validator.Validate(cmd, out var reason))
                return CommandResult.Reject(reason);

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
