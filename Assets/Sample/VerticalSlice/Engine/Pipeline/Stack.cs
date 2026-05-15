#nullable enable
using System;
using System.Threading.Tasks;
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Entities;
using SysStack = System.Collections.Generic.Stack<VerticalSlice.Engine.Pipeline.StackFrame>;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// The engine's effect stack. LIFO container for effects awaiting
    /// resolution — casts pushed by <c>PlayCard</c>-style commands,
    /// scheduled reactions from After-phase hooks. Commands never touch
    /// the stack; only effects live here.
    ///
    /// <para><b>The stack owns its own policy + resolution.</b> The engine
    /// only knows two things about us: "do you have work?" (via the gate
    /// check inside <see cref="ResolveNextAsync"/>) and "let me know when
    /// you're done" (the returned Task). The engine never pops, never
    /// inspects frames, never decides when we resolve. We do.</para>
    ///
    /// <para>Default class ships HS-style: any non-empty stack auto-resolves,
    /// popping one frame at a time. Games swap in a subclass to change
    /// behaviour — override <see cref="ShouldResolve"/> to gate (MTG's
    /// "both players passed priority"), override <see cref="ResolveAsync"/>
    /// to customize what happens on a pop. For animations / network /
    /// other real async, <see cref="ResolveAsync"/> can await — the engine
    /// pump awaits us back, no flag-juggling needed.</para>
    ///
    /// <para>For "wait for player input" the right tool is
    /// <see cref="Effects.IEffectScope.OpenRequest"/>, not async — the
    /// player response arrives as a pipeline command and resumes the
    /// pump naturally.</para>
    ///
    /// <para>Replace via <c>engine.Stack = new MyStack(engine);</c> at bootstrap.</para>
    /// </summary>
    public class Stack
    {
        private readonly SysStack _frames = new();

        /// <summary>The owning engine. Subclasses can reach it for
        /// scope access, world queries, etc.</summary>
        protected GameEngine Engine { get; }

        public Stack(GameEngine engine) { Engine = engine; }

        public int Count => _frames.Count;
        public StackFrame Peek() => _frames.Peek();

        /// <summary>
        /// Add an effect to the top of the stack. Kicks the engine — if
        /// pushed from inside an active pump cycle (a hook firing during
        /// Apply, a sub-effect inside a resolving frame) Kick is a no-op
        /// because the pump is already running. If pushed from outside
        /// (e.g. a test pushing a frame directly), Kick starts the pump
        /// so the frame actually resolves.
        /// </summary>
        public void Push(StackFrame frame)
        {
            _frames.Push(frame);
            Engine.Kick();
        }

        /// <summary>
        /// Optional cleanup hook fired after a frame with a non-null
        /// <see cref="StackFrame.SourceCard"/> resolves. The slice's
        /// bootstrap wires this to move the spell card to its owner's
        /// graveyard. Triggered effects (SourceCard = null) bypass it.
        /// </summary>
        public Action<IEntity>? OnCastResolved { get; set; }

        // --- Game-overridable policy + resolution ---------------------------

        /// <summary>
        /// Should the stack pop and resolve a frame right now? Default
        /// returns true whenever the stack is non-empty (slice / HS-style
        /// auto-resolve). MTG-like games override to also require a
        /// "both players passed priority" condition.
        /// </summary>
        protected virtual bool ShouldResolve() => _frames.Count > 0;

        /// <summary>
        /// Resolve a popped frame. Default: run the effect inline through
        /// the engine's effect service, then fire <see cref="OnCastResolved"/>
        /// if the frame carries a source card. Synchronous — returns a
        /// completed Task. Override to await real async work (animations,
        /// network, etc.). The engine pump awaits this Task before moving
        /// on, so no flag-juggling is needed: the stack reports busy by
        /// simply not completing the Task yet.
        /// </summary>
        protected virtual Task ResolveAsync(StackFrame frame)
        {
            RunEffect(frame.Effect, frame.ParentBlockId);
            if (frame.SourceCard != null)
                OnCastResolved?.Invoke(frame.SourceCard);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Run an effect through the engine's effect service. Available
        /// to subclasses overriding <see cref="ResolveAsync"/> from another
        /// assembly — engine internals stay internal; this is the
        /// controlled path.
        /// </summary>
        protected void RunEffect(Effect effect, long? parentBlockId = null)
            => Engine.Effects.RunInline(effect, parentBlockId);

        // --- Engine orchestration -------------------------------------------

        /// <summary>
        /// Engine asks the stack to resolve one frame, if it wants to.
        /// We check our own policy (<see cref="ShouldResolve"/>), pop, and
        /// await <see cref="ResolveAsync"/>. Returns a Task that completes
        /// when the resolution is done; the Task's result tells the engine
        /// whether progress was made (true = a frame resolved). Engine
        /// uses that to decide whether to keep evaluating.
        ///
        /// <para>If we have no work right now, the returned Task completes
        /// immediately with false — engine moves on.</para>
        /// </summary>
        internal async Task<bool> ResolveNextAsync()
        {
            if (!ShouldResolve()) return false;
            var frame = _frames.Pop();
            await ResolveAsync(frame);
            return true;
        }
    }
}
