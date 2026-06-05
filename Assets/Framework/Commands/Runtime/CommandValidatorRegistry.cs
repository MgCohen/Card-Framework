using System;
using System.Collections.Generic;

namespace Framework.Commands
{
    public sealed class CommandValidatorRegistry
    {
        readonly Dictionary<Type, ICommandValidator> _byType = new();

        public CommandValidatorRegistry(IReadOnlyList<ICommandValidator> validators)
        {
            for (var i = 0; i < validators.Count; i++)
            {
                var v = validators[i];
                if (_byType.ContainsKey(v.CommandType))
                    throw new InvalidOperationException(
                        $"Duplicate command validator for {v.CommandType.Name}; a command has at most one validator.");
                _byType[v.CommandType] = v;
            }
        }

        // Opt-in: absence is legal — a command with no registered validator is accepted.
        // Hence a try-get rather than the handler registry's throw-on-miss.
        public bool TryFor(Type commandType, out ICommandValidator validator)
            => _byType.TryGetValue(commandType, out validator);
    }
}
