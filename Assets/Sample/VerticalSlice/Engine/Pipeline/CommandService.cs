#nullable enable
using System.Collections.Generic;
using VerticalSlice.Engine.Commands;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Stream;
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// Runs a command. <b>Two-phase dispatch via the stack.</b> Per command:
    /// <list type="number">
    /// <item>Open a Block on the wire.</item>
    /// <item><b>Before phase</b> — iterate hooks registered in
    /// <see cref="EffectStore"/> for <c>(TCommand, Before)</c> in priority
    /// order; for each whose <see cref="HookConfig.IsActive"/> returns
    /// true, build the effect via the factory, push it onto the
    /// <see cref="Stack"/>, and synchronously drain. Resolved effects
    /// run nested inside this command's block — replacement-style hooks
    /// (DivineShield zeroes Amount, Armor reduces it) mutate the cmd
    /// before Apply sees it.</item>
    /// <item>Call <c>command.Apply(cmdScope)</c>.</item>
    /// <item><b>After phase</b> — same iteration, but push without
    /// draining. Frames sit on the stack with this command's block id as
    /// their parent. The engine pump drains them later via
    /// <c>Evaluate</c> → they resolve at world depth 0, becoming wire
    /// siblings of this command (Backlash, Deathrattle, etc.), with
    /// <c>ParentId</c> threading back here for causality.</item>
    /// <item>Close the Block.</item>
    /// </list>
    ///
    /// <para><b>One path to running a command, regardless of caller.</b>
    /// External callers reach <see cref="Execute"/> only through the
    /// engine pipeline (<see cref="GameEngine.Enqueue"/>); effects, hooks,
    /// and the SBA pass call it directly. No <c>IsPumping</c> branch, no
    /// dual surface.</para>
    /// </summary>
    public sealed class CommandService
    {
        private readonly World _world;
        private EffectService? _effects;
        private GameEngine? _engine;
        private EffectStore? _store;

        public CommandService(World world) { _world = world; }

        internal void BindEffects(EffectService effects) => _effects = effects;
        internal void BindEngine(GameEngine engine) => _engine = engine;
        internal void BindStore(EffectStore store) => _store = store;

        /// <summary>
        /// Run a command. See class docs for the full dispatch sequence.
        /// <paramref name="parentOverride"/> is the rare-case knob for
        /// callers that want the BlockStart to record a parent other than
        /// the currently open block; defaults to "use whatever's open."
        /// </summary>
        public void Execute(Command command, long? parentOverride = null)
        {
            var (id, parentId) = _world.OpenBlock(parentOverride);
            _world.Log.Append(new BlockStartEvent(
                id, parentId, BlockKind.Command, command.GetType().Name,
                PayloadCapture.ExtractSource(command),
                PayloadCapture.Of(command)));
            var outcome = BlockOutcome.Completed;
            try
            {
                var cmdScope = new CommandScope(_world, _store!, _engine!.Stack);
                Dispatch(command, HookPhase.Before, drainEach: true);
                command.Apply(cmdScope);
                Dispatch(command, HookPhase.After, drainEach: false);
            }
            catch
            {
                outcome = BlockOutcome.Errored;
                throw;
            }
            finally
            {
                var closingId = _world.CloseBlock();
                _world.Log.Append(new BlockEndEvent(closingId, outcome));
            }
        }

        // --- Hook dispatch ---------------------------------------------------

        private void Dispatch(Command command, HookPhase phase, bool drainEach)
        {
            var subs = _store!.Subscribers(command.GetType(), phase);
            if (subs.Count == 0) return;

            var scope = _effects!.NewScope();
            var ctx = new HookContext { Command = command, Scope = scope };
            long? parentForPush = drainEach ? null : _world.CurrentBlockId;

            foreach (var sub in subs)
            {
                if (!sub.Config.IsActive(in ctx)) continue;
                var effect = sub.Factory(command);
                _engine!.Stack.Push(new StackFrame(effect, parentBlockId: parentForPush));
                if (drainEach)
                {
                    // Synchronous drain — the base Stack's ResolveAsync returns a
                    // completed Task, so this completes inline. Async stack
                    // subclasses doing real awaits in Before-phase would block
                    // here; acceptable for the slice. Pump Evaluate handles
                    // After-phase drain in proper async fashion.
                    _engine.Stack.ResolveNextAsync().GetAwaiter().GetResult();
                }
            }
        }

        // --- Internal scope ---------------------------------------------------

        private sealed class CommandScope : ICommandScope
        {
            private readonly World _world;
            public EffectStore EffectStore { get; }
            public Stack Stack { get; }

            public CommandScope(World w, EffectStore store, Stack stack)
            {
                _world = w;
                EffectStore = store;
                Stack = stack;
            }

            public Entity? Resolve(IEntity entity) => _world.Get(entity.Id);
            public Entity? Get(int id) => _world.Get(id);
            public IEnumerable<Entity> All() => _world.All();
            public IEnumerable<Entity> Where(TagKey<bool> marker) => _world.Where(marker);
        }
    }
}
