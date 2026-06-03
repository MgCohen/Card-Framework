using System;

namespace Framework.Commands
{
    // Typed authoring base for command logic — the analogue of WireEventHandler<T>.
    // Holds the orchestration for one command type and ctor-injects the services it calls
    // (the load-bearing reason for the command/handler split: injection is paid once at
    // startup, commands stay pure data, and the service/DI boundary lives only here). The
    // bridge below lets the registry dispatch through the non-generic ICommandHandler.
    public abstract class CommandHandler<T> : ICommandHandler where T : Command
    {
        public Type CommandType => typeof(T);

        public abstract void Handle(T cmd);

        void ICommandHandler.Handle(Command cmd) => Handle((T)cmd);
    }
}
