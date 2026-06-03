using System;
using System.Collections.Generic;

namespace Framework.Commands
{
    // Maps command type -> its single authoritative handler, collected from DI.
    // Mirrors Framework.Wire's HandlerRegistry, but a command has exactly one handler
    // (unlike a wire event, which may fan out to many), so duplicate registration is an error.
    public sealed class CommandHandlerRegistry
    {
        readonly Dictionary<Type, ICommandHandler> _byType = new();

        public CommandHandlerRegistry(IReadOnlyList<ICommandHandler> handlers)
        {
            for (var i = 0; i < handlers.Count; i++)
            {
                var h = handlers[i];
                if (_byType.ContainsKey(h.CommandType))
                    throw new InvalidOperationException(
                        $"Duplicate command handler for {h.CommandType.Name}; a command has exactly one handler.");
                _byType[h.CommandType] = h;
            }
        }

        public ICommandHandler HandlerFor(Type commandType)
        {
            if (!_byType.TryGetValue(commandType, out var handler))
                throw new InvalidOperationException($"No command handler registered for {commandType.Name}.");
            return handler;
        }
    }
}
