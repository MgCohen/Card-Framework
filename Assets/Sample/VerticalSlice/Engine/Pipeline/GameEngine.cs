#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VerticalSlice.Engine.Commands;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// Composition root + orchestrator. Owns four primitives:
    ///
    /// <list type="bullet">
    /// <item><b>Pipeline</b> — FIFO of external arrivals. Player input /
    /// system events enter via <see cref="Enqueue"/>.</item>
    /// <item><b>Stack</b> (<see cref="Stack"/>) — LIFO of effects awaiting
    /// resolution. Owns its own policy + resolution via
    /// <see cref="Stack.ResolveNextAsync"/>; engine just awaits.</item>
    /// <item><b>EffectStore</b> (<see cref="EffectStore"/>) — single
    /// registry for hook subscriptions (command-typed) and direct
    /// triggers (entity-keyed). <c>CommandService</c> reads hook subs
    /// from it during dispatch; <c>FireDirect</c> reads direct factories
    /// from it; <c>PlayCard</c>-style commands look up OnCast factories
    /// from it.</item>
    /// <item><b>SBAs</b> — state-based actions. Run inline inside
    /// <see cref="EvaluateAsync"/>.</item>
    /// </list>
    ///
    /// <para>Pump loop: dequeue a command, run it, evaluate (SBAs +
    /// stack), repeat until both pipeline and stack rest. Every step is
    /// awaitable.</para>
    ///
    /// <para><b>Two surfaces, not one.</b> External callers use
    /// <see cref="Enqueue"/>; code inside the engine (effects, hooks, the
    /// pump itself) calls <see cref="CommandService.Execute"/>. The
    /// pipeline is for arrivals; <c>Execute</c> is for execution.</para>
    /// </summary>
    public sealed class GameEngine
    {
        public World World { get; }
        public CommandService Commands { get; }
        public EffectService Effects { get; }
        public RequestsService Requests { get; }
        public EffectStore EffectStore { get; }

        /// <summary>
        /// The effect stack. Games may swap this at bootstrap to change
        /// policy / resolution semantics.
        /// </summary>
        public Stack Stack { get; set; }

        private readonly Queue<Command> _pipeline = new();
        private readonly List<ISBA> _sbas = new();
        private bool _pumping;

        public bool IsPumping => _pumping;

        public GameEngine()
        {
            World = new World();
            EffectStore = new EffectStore();
            Commands = new CommandService(World);
            Effects = new EffectService(World, Commands);
            Requests = new RequestsService(World, Effects);
            Stack = new Stack(this);
            Commands.BindEffects(Effects);
            Commands.BindStore(EffectStore);
            Commands.BindEngine(this);
            Effects.BindStore(EffectStore);
            Effects.BindRequests(Requests);
            Effects.BindEngine(this);
        }

        public void RegisterSBA(ISBA sba) => _sbas.Add(sba);

        // --- External entry --------------------------------------------------

        public void Enqueue(Command command)
        {
            _pipeline.Enqueue(command);
            Kick();
        }

        internal void Kick()
        {
            if (_pumping) return;
            var task = RunPumpAsync();
            if (task.IsCompleted) task.GetAwaiter().GetResult();
        }

        // --- Pump ------------------------------------------------------------

        private async Task RunPumpAsync()
        {
            _pumping = true;
            try
            {
                await EvaluateAsync();
                while (_pipeline.Count > 0)
                {
                    var cmd = _pipeline.Dequeue();
                    Commands.Execute(cmd);
                    await EvaluateAsync();
                }
            }
            finally { _pumping = false; }
        }

        private async Task EvaluateAsync()
        {
            while (true)
            {
                bool progress = false;

                if (_sbas.Count > 0)
                {
                    var scope = Effects.NewScope();
                    foreach (var sba in _sbas)
                    {
                        var emissions = new List<Command>(sba.Evaluate(scope));
                        foreach (var cmd in emissions)
                        {
                            Commands.Execute(cmd);
                            progress = true;
                        }
                    }
                }

                if (await Stack.ResolveNextAsync()) progress = true;

                if (!progress) break;
            }
        }
    }
}
