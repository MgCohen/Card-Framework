using System;

namespace Framework.Commands
{
    public interface ICommandHandler
    {
        Type CommandType { get; }
        void Handle(Command cmd);
    }
}
