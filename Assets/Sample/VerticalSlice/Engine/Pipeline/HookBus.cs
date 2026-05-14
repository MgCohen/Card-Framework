#nullable enable
using System;
using System.Collections.Generic;
using VerticalSlice.Engine.Commands;
using VerticalSlice.Engine.Hooks;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// Per-command-type, per-phase, priority-sorted hook registry. Owned
    /// by <see cref="CommandService"/>. Subscriptions are keyed by exact
    /// command type — no inheritance walk; <c>Subscribe&lt;DealDamage&gt;</c>
    /// only fires for <c>DealDamage</c>, not subclasses.
    ///
    /// Handlers run in <see cref="HookPriority.Order"/> ascending. Within a
    /// priority band, registration order is preserved (FIFO).
    /// </summary>
    internal sealed class HookBus
    {
        private readonly Dictionary<(Type, HookPhase), List<Subscription>> _subs = new();

        public void Subscribe<TCommand>(HookPhase phase, HookPriority priority,
                                        HookHandler<TCommand> handler)
            where TCommand : Command
        {
            var key = (typeof(TCommand), phase);
            if (!_subs.TryGetValue(key, out var list))
            {
                list = new List<Subscription>();
                _subs[key] = list;
            }
            list.Add(new Subscription(priority, (cmd, scope) => handler((TCommand)cmd, scope)));
            list.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        public void Fire(Command command, HookPhase phase, IHookScope scope)
        {
            var key = (command.GetType(), phase);
            if (!_subs.TryGetValue(key, out var list)) return;
            // Snapshot — handlers may register more subscriptions or run other
            // commands which fire their own hooks; we don't want to mutate the
            // collection mid-iteration.
            var snap = list.ToArray();
            foreach (var sub in snap) sub.Invoke(command, scope);
        }

        private readonly struct Subscription
        {
            public HookPriority Priority { get; }
            private readonly Action<Command, IHookScope> _handler;

            public Subscription(HookPriority priority, Action<Command, IHookScope> handler)
            {
                Priority = priority;
                _handler = handler;
            }

            public void Invoke(Command cmd, IHookScope scope) => _handler(cmd, scope);
        }
    }
}
