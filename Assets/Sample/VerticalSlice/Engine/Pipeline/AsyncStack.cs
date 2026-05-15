#nullable enable
using System.Threading.Tasks;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// Base for stacks that need to run real <see cref="Task"/>-based
    /// async work between effects — MTG-style "wait for both players to
    /// pass priority," animation pauses, network round-trips, etc. The
    /// engine's pump stays synchronous; async lives entirely inside the
    /// stack subclass.
    ///
    /// <para>How it integrates with the sync pump:
    /// <list type="number">
    /// <item>Engine asks <see cref="Stack.TryResolveOne"/>. The base
    /// calls <see cref="ShouldResolve"/> — we report "at rest" while a
    /// previous async resolution is still in flight.</item>
    /// <item>If we say go, the engine calls our (sealed) <see cref="Resolve"/>,
    /// which kicks off <see cref="ResolveAsync"/> as fire-and-forget. We
    /// immediately set our "pending" flag and return. Engine sees the
    /// resolve happened and continues its Evaluate loop; next time it
    /// checks the stack, we report "at rest" again. Pump drains pipeline,
    /// idles.</item>
    /// <item>When the async work completes, we clear the flag and
    /// <see cref="GameEngine.Kick"/> the pump. Engine resumes; on the
    /// next Evaluate pass it asks again and we resolve the next frame.</item>
    /// </list>
    /// </para>
    ///
    /// <para>Subclasses implement <see cref="ResolveAsync"/>. Call
    /// <see cref="Stack.RunEffect"/> at the point inside the body where
    /// the wire block should open / close. <see cref="Stack.OnCastResolved"/>
    /// fires automatically after <see cref="ResolveAsync"/> completes if
    /// the frame's <see cref="StackFrame.SourceCard"/> is non-null —
    /// same contract as the base sync <see cref="Stack.Resolve"/>.</para>
    ///
    /// <para><b>Threading caveat.</b> The engine pump is not thread-safe.
    /// Task continuations resumed via <c>Engine.Kick()</c> must run on the
    /// same context that drove the original pump (Unity's main thread, in
    /// a typical setup). Either run with a SynchronizationContext that
    /// posts continuations to the main thread (default in Unity), or use
    /// <c>ConfigureAwait(true)</c> explicitly. Don't fan out across
    /// ThreadPool threads inside <see cref="ResolveAsync"/>.</para>
    ///
    /// <para><b>When NOT to use this.</b> For "effect needs input from the
    /// player" — open a Request in the effect body and return. The
    /// player's response arrives as a pipeline command (Respond), the
    /// engine resumes naturally. No async needed. AsyncStack is for cases
    /// where the wait isn't a player decision but a real time-bound
    /// operation: animations finishing, network round-trips, ML inference,
    /// etc.</para>
    /// </summary>
    public abstract class AsyncStack : Stack
    {
        private Task? _pending;

        protected AsyncStack(GameEngine engine) : base(engine) { }

        /// <summary>
        /// Gate: only allow a pop when no async resolution is currently
        /// in flight. The base policy (non-empty stack) still applies.
        /// </summary>
        protected override bool ShouldResolve()
            => _pending == null && base.ShouldResolve();

        /// <summary>
        /// Sealed: the async flavor is the only way subclasses customize
        /// resolution. Implement <see cref="ResolveAsync"/> instead.
        /// </summary>
        protected sealed override void Resolve(StackFrame frame)
        {
            _pending = RunAsyncLifecycle(frame);
        }

        private async Task RunAsyncLifecycle(StackFrame frame)
        {
            try
            {
                await ResolveAsync(frame);
                if (frame.SourceCard != null)
                    OnCastResolved?.Invoke(frame.SourceCard);
            }
            finally
            {
                _pending = null;
                Engine.Kick();
            }
        }

        /// <summary>
        /// Implement the async resolution body. Typical shape:
        /// <code>
        /// protected override async Task ResolveAsync(StackFrame frame)
        /// {
        ///     await WaitForPriorityPasses();           // game-specific
        ///     RunEffect(frame.Effect, frame.ParentBlockId);
        ///     await AnimateImpact(frame.Effect);       // optional
        /// }
        /// </code>
        /// Call <see cref="Stack.RunEffect"/> at the moment the wire
        /// block should open. The base class fires
        /// <see cref="Stack.OnCastResolved"/> after this Task completes
        /// (for non-null <see cref="StackFrame.SourceCard"/>) — don't
        /// fire it yourself.
        /// </summary>
        protected abstract Task ResolveAsync(StackFrame frame);
    }
}
