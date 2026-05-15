#nullable enable
using System;
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
    /// The stack owns its own policy + resolution. The engine doesn't
    /// know when or how the stack resolves — it just calls
    /// <see cref="TryResolveOne"/> between pipeline commands and asks
    /// "take over if you want." Default class (this one) ships HS-style:
    /// any non-empty stack auto-resolves, popping one frame at a time
    /// through <see cref="Resolve"/>. Games swap in a subclass to change
    /// behaviour — override <see cref="ShouldResolve"/> to gate (MTG's
    /// "both players passed priority"), override <see cref="Resolve"/> to
    /// customize what happens on a pop.
    ///
    /// For async resolution (animation pauses, network round-trips, etc.),
    /// subclass <see cref="AsyncStack"/> instead — it wires up the "gate
    /// while async work is in flight + kick the engine when done"
    /// lifecycle so subclasses only implement <c>ResolveAsync</c>. Note
    /// that for "wait for player input" the right tool is
    /// <see cref="Effects.IEffectScope.OpenRequest"/>, not async — the
    /// player response arrives as a pipeline command and resumes the
    /// pump naturally.
    ///
    /// Replace via <c>engine.Stack = new MyStack(engine);</c> at bootstrap.
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
        /// Add an effect to the top of the stack. Kicks the engine pump —
        /// if this push came from outside any active pump frame, the pump
        /// starts and the resolve loop will call <see cref="TryResolveOne"/>
        /// after its first SBA pass.
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
        /// if the frame carries a source card. Override to weave in extra
        /// wire emissions, post-resolve checks, etc.
        /// </summary>
        protected virtual void Resolve(StackFrame frame)
        {
            RunEffect(frame.Effect, frame.ParentBlockId);
            if (frame.SourceCard != null)
                OnCastResolved?.Invoke(frame.SourceCard);
        }

        /// <summary>
        /// Run an effect through the engine's effect service. Available
        /// to subclasses overriding <see cref="Resolve"/> from another
        /// assembly — engine internals stay internal; this is the
        /// controlled path.
        /// </summary>
        protected void RunEffect(Effect effect, long? parentBlockId = null)
            => Engine.Effects.RunInline(effect, parentBlockId);

        // --- Engine orchestration -------------------------------------------

        /// <summary>
        /// Engine calls this between pipeline commands. The stack decides
        /// (via <see cref="ShouldResolve"/>) whether to pop, and how (via
        /// <see cref="Resolve"/>) to handle the popped frame. Returns
        /// true if work happened — caller (engine Evaluate loop) uses
        /// that to decide whether to re-evaluate or move on.
        /// </summary>
        internal bool TryResolveOne()
        {
            if (!ShouldResolve()) return false;
            var frame = _frames.Pop();
            Resolve(frame);
            return true;
        }
    }
}
