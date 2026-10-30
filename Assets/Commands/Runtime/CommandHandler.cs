using System;

namespace Framework.Commands
{
    public abstract class CommandHandler<T> : ICommandHandler where T : Command
    {
        public Type CommandType => typeof(T);

        public abstract void Handle(T cmd);

        void ICommandHandler.Handle(Command cmd) => Handle((T)cmd);
    }
}
