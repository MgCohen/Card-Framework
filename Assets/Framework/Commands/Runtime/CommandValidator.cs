using System;

namespace Framework.Commands
{
    public abstract class CommandValidator<T> : ICommandValidator where T : Command
    {
        public Type CommandType => typeof(T);

        public abstract bool Validate(T cmd, out string reason);

        bool ICommandValidator.Validate(Command cmd, out string reason) => Validate((T)cmd, out reason);
    }
}
