using System;

namespace Framework.Commands
{
    // Non-generic handler contract — what CommandHandlerRegistry collects and dispatches by
    // type. Mirrors IWireEventHandler in Framework.Wire: a CommandType discriminator plus a
    // boxed-entry Handle. Authors derive from the typed CommandHandler<T> base, not this.
    public interface ICommandHandler
    {
        Type CommandType { get; }
        void Handle(Command cmd);
    }
}
