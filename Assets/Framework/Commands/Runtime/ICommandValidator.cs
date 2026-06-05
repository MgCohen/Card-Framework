using System;

namespace Framework.Commands
{
    public interface ICommandValidator
    {
        Type CommandType { get; }
        bool Validate(Command cmd, out string reason);
    }
}
