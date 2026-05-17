#nullable enable
using System.Collections.Generic;

namespace StateMachine.Engine
{
    /// <summary>
    /// The orchestrator. Holds the FIFO pipeline, the world, the registries.
    /// After every command resolves, runs the Evaluate hook (spike §v6):
    /// if current.IsCompleted, ask the flow for a successor and enqueue a
    /// PushStateCommand.
    ///
    /// <para>External-entry method is deliberately Enqueue (not Execute/Submit)
    /// to leave room for a future multi-type pipeline.</para>
    /// </summary>
    public sealed class GameEngine
    {
        public World            World    { get; }
        public HandlerRegistry  Handlers { get; }
        public IStateRegistry   States   { get; }
        public IStateFlow       Flow     { get; }

        // Filled in by the bootstrap once the service has a back-reference.
        public StateService     State    { get; set; } = null!;

        private readonly Queue<GameAction> _queue = new();
        private bool _draining;

        public GameEngine(World world, HandlerRegistry handlers, IStateRegistry states, IStateFlow flow)
        {
            World = world;
            Handlers = handlers;
            States = states;
            Flow = flow;
        }

        /// <summary>Submit a command. Drains the queue inline (sync engine).</summary>
        public void Enqueue(GameAction cmd)
        {
            _queue.Enqueue(cmd);
            if (_draining) return;
            Drain();
        }

        private void Drain()
        {
            _draining = true;
            try
            {
                while (_queue.Count > 0)
                {
                    var cmd   = _queue.Dequeue();
                    var scope = new CommandScope(this);
                    var invoker = Handlers.Resolve(cmd);

                    var result = invoker.Invoke(cmd, scope);
                    scope.Emit(new CommandExecutedEvent(cmd.GetType().Name));

                    Evaluate(cmd, result);
                }
            }
            finally { _draining = false; }
        }

        /// <summary>
        /// Spike §v6 hook: after a command resolves, ask the current state
        /// whether it's completed; if so, enqueue a PushStateCommand for the
        /// flow's successor. PushStateCommand is *itself* a command, so this
        /// keeps the model uniform — transitions are not a parallel path.
        /// </summary>
        private void Evaluate(GameAction action, CommandResult result)
        {
            // Don't re-evaluate during a push — the push handler is already
            // managing the swap. Otherwise we'd recurse on the new state's
            // OnEnter outputs.
            if (action is PushStateCommand) return;

            var current = State.Current;
            var scope = new StateScope(World, current);
            if (!current.IsCompleted(action, result, scope)) return;

            var next = Flow.NextAfter(current, scope);
            if (next == null) return;

            _queue.Enqueue(new PushStateCommand(next));
        }

        internal void AppendToWire(WireEvent ev) => World.Log.Append(ev);
        internal void EnqueueFollowup(GameAction cmd) => _queue.Enqueue(cmd);
    }

    internal sealed class CommandScope : ICommandScope
    {
        private readonly GameEngine _engine;
        public CommandScope(GameEngine engine) { _engine = engine; }
        public World World => _engine.World;
        public void Enqueue(GameAction command) => _engine.EnqueueFollowup(command);
        public void Emit(WireEvent ev) => _engine.AppendToWire(ev);
    }
}
