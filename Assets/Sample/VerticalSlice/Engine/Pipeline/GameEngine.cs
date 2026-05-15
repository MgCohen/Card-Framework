#nullable enable
using System;
using System.Collections.Generic;
using VerticalSlice.Engine.Commands;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// Composition root + main loop. Two collections plus an evaluate step:
    ///
    /// <list type="bullet">
    /// <item><b>Pipeline</b> (<c>Queue&lt;Command&gt;</c>) — strict FIFO.
    /// External commands land here: player input via
    /// <see cref="CommandService.Execute"/>, system commands. Drained one
    /// at a time by the pump.</item>
    /// <item><b>Stack</b> (<see cref="Stack"/>) — LIFO of effects. <b>The
    /// stack owns its own policy and resolution mechanics</b>; the engine
    /// just calls <see cref="Stack.TryResolveOne"/> between pipeline
    /// commands and asks "take over if you want." Default = HS-style
    /// auto-resolve. Games swap in subclasses for MTG-style priority
    /// gates, etc.</item>
    /// <item><b>SBAs</b> — state-based actions that yield commands.
    /// Emissions run inline inside <see cref="Evaluate"/> so cleanup
    /// stays atomic with the triggering work.</item>
    /// </list>
    ///
    /// The pump loop is simple: take one pipeline command, run it inline,
    /// evaluate. Evaluate itself loops: SBAs apply, then ask the stack
    /// to resolve one. When evaluate stabilizes, return to the pump; when
    /// the pipeline drains and the stack is at rest, the engine idles.
    ///
    /// External code only enters via <see cref="CommandService.Execute"/>
    /// or <see cref="Stack.Push"/> — no <c>Effects.Run</c> equivalent.
    /// Effects only run by being popped from the stack (or by being
    /// inline-run via <c>scope.Run</c> / <c>IHookScope.Run</c> from
    /// within a resolving frame).
    /// </summary>
    public sealed class GameEngine
    {
        public World World { get; }
        public CommandService Commands { get; }
        public EffectService Effects { get; }
        public RequestsService Requests { get; }
        public CardEffectStore CardEffects { get; }

        /// <summary>
        /// The effect stack. Games may swap this for a subclass at
        /// bootstrap to change policy / resolution semantics
        /// (e.g. MTG-style priority gating). Replace before any pump
        /// activity.
        /// </summary>
        public Stack Stack { get; set; }

        private readonly Queue<Command> _pipeline = new();
        private readonly List<ISBA> _sbas = new();
        private bool _pumping;

        /// <summary>True while a pump frame is active. Re-entrant pushes
        /// (from inside Apply / Effect.Run / hooks) skip the pump kick;
        /// the outer loop picks up the new work.</summary>
        public bool IsPumping => _pumping;

        public GameEngine()
        {
            World = new World();
            CardEffects = new CardEffectStore();
            Commands = new CommandService(World);
            Effects = new EffectService(World, Commands, CardEffects);
            Requests = new RequestsService(World, Effects);
            Stack = new Stack(this);
            Commands.BindEffects(Effects);
            Effects.BindRequests(Requests);
            Commands.BindEngine(this);
            Effects.BindEngine(this);
        }

        public void RegisterSBA(ISBA sba) => _sbas.Add(sba);

        // --- Pump entry points -----------------------------------------------

        /// <summary>
        /// Pipeline enqueue + pump kick. Called by
        /// <see cref="CommandService.Execute"/> when invoked from outside
        /// any active pump frame.
        /// </summary>
        internal void EnqueuePipeline(Command cmd)
        {
            _pipeline.Enqueue(cmd);
            Kick();
        }

        /// <summary>
        /// Start the pump loop if not already running. Idempotent — a
        /// re-entrant call (already pumping) returns immediately; the
        /// active loop will pick up whatever was just enqueued / pushed.
        /// </summary>
        internal void Kick()
        {
            if (_pumping) return;
            RunPump(null);
        }

        /// <summary>
        /// Run a body inside a pump frame. If the engine is already
        /// pumping, just executes <paramref name="body"/> inline (re-entrant
        /// scope.Execute / Run will route correctly because IsPumping is
        /// already true). Otherwise opens a fresh frame: runs the body,
        /// then drains pipeline + evaluates until the world is at rest.
        /// Used by <see cref="EffectService.RunResolution"/> so a request
        /// response's body executes as one cohesive resolution.
        /// </summary>
        internal void EnterPumpFrame(Action body)
        {
            if (_pumping) { body(); return; }
            RunPump(body);
        }

        private void RunPump(Action? initialBody)
        {
            _pumping = true;
            try
            {
                initialBody?.Invoke();
                Evaluate();
                while (_pipeline.Count > 0)
                {
                    var cmd = _pipeline.Dequeue();
                    Commands.RunInline(cmd);
                    Evaluate();
                }
            }
            finally { _pumping = false; }
        }

        // --- Evaluate --------------------------------------------------------

        /// <summary>
        /// The world-stabilization step. Run after every pipeline command
        /// and at the start of every fresh pump frame. Loops:
        /// <list type="number">
        /// <item>Every registered SBA evaluates; each yielded command runs
        /// inline (sibling top-level wire block — death cleanup, etc.).</item>
        /// <item>Ask the stack: take over if you want. The stack decides
        /// policy + resolution internally (<see cref="Stack.TryResolveOne"/>).
        /// </item>
        /// </list>
        /// Re-loops until a pass produces no progress. SBA cascades + stack
        /// chains converge in one Evaluate call.
        /// </summary>
        internal void Evaluate()
        {
            while (true)
            {
                bool progress = false;

                // 1. SBAs — cleanup commands run inline (atomic with the
                //    triggering work; not interleaved with player input).
                if (_sbas.Count > 0)
                {
                    var scope = Effects.NewScope();
                    foreach (var sba in _sbas)
                    {
                        // Materialize so the SBA sees a stable snapshot
                        // before we start running commands that mutate state.
                        var emissions = new List<Command>(sba.Evaluate(scope));
                        foreach (var cmd in emissions)
                        {
                            Commands.RunInline(cmd);
                            progress = true;
                        }
                    }
                }

                // 2. Stack — engine just asks; stack owns policy + execution.
                if (Stack.TryResolveOne()) progress = true;

                if (!progress) break;
            }
        }
    }
}
