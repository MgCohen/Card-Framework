#nullable enable
using System;
using System.Collections.Generic;
using VerticalSlice.Engine.Commands;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// Imperative hook registry. Subscribe by (command type, phase, priority);
    /// the bus fires handlers in priority order for the exact runtime command
    /// type (no inheritance-aware dispatch — register on each type you want
    /// to react to).
    /// </summary>
    public sealed class SignalBus
    {
        private sealed class Subscription
        {
            public HookPriority Priority { get; }
            public Action<Command> Handler { get; }
            public Subscription(HookPriority priority, Action<Command> handler)
            {
                Priority = priority;
                Handler = handler;
            }
        }

        private readonly Dictionary<(Type cmdType, HookPhase phase), List<Subscription>> _subs = new();

        public void Subscribe<TCmd>(HookPhase phase, HookPriority priority, Action<TCmd> handler)
            where TCmd : Command
        {
            var key = (typeof(TCmd), phase);
            if (!_subs.TryGetValue(key, out var list))
            {
                list = new List<Subscription>();
                _subs[key] = list;
            }

            list.Add(new Subscription(priority, cmd => handler((TCmd)cmd)));
            list.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        public void Fire(Command cmd, HookPhase phase)
        {
            if (!_subs.TryGetValue((cmd.GetType(), phase), out var list)) return;

            // Snapshot to allow handlers to subscribe/unsubscribe mid-fire.
            var snapshot = list.ToArray();
            foreach (var sub in snapshot)
                sub.Handler(cmd);
        }
    }
}
