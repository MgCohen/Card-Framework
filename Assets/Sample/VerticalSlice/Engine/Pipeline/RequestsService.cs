#nullable enable
using System.Collections.Generic;
using VerticalSlice.Engine.Effects;
using VerticalSlice.Engine.Stream;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// Holds open player-decision requests. Authors don't reach into this
    /// directly: effects call <see cref="IEffectScope.OpenRequest"/>, the
    /// host (Unity UI / tests) reads <see cref="Open"/> each frame to
    /// render the picker, and the host calls <see cref="Respond"/> with
    /// the player's pick.
    ///
    /// Sync model. Effects that open a request return immediately; the
    /// request sits in <see cref="Open"/> until Respond fires the
    /// chosen option's server-side resolution through the EffectService
    /// scope.
    /// </summary>
    public sealed class RequestsService
    {
        private readonly World _world;
        private readonly EffectService _effects;
        private readonly Dictionary<int, Request> _open = new();
        private int _nextId = 1;

        public RequestsService(World world, EffectService effects)
        {
            _world = world;
            _effects = effects;
        }

        public IReadOnlyDictionary<int, Request> Open => _open;

        internal void OpenRequestInternal(Request request)
        {
            request.Id = _nextId++;

            // Open the Request block, emit each option as its typed wire
            // event, close. The block has no "body" the way an Effect
            // does — its body IS the option declarations. Close right
            // after emitting them.
            var (blockId, parentId) = _world.OpenBlock();
            _world.Log.Append(new BlockStartEvent(
                blockId, parentId, BlockKind.Request, request.Name,
                source: null,
                payload: new Dictionary<string, object?>
                {
                    ["RequestId"] = request.Id,
                }));

            foreach (var option in request.Options)
                _world.Log.Append(option);

            var closingId = _world.CloseBlock();
            _world.Log.Append(new BlockEndEvent(closingId, BlockOutcome.Completed));

            _open[request.Id] = request;
        }

        /// <summary>
        /// Resolve an open request with the player's pick. The picked
        /// index points into the request's <see cref="Request.Options"/>
        /// list. Emits a <see cref="ResponseEvent"/>, then invokes
        /// <see cref="Request.Resolve"/> through an EffectService-built
        /// scope so any commands / effects it dispatches get standard
        /// wire treatment.
        /// </summary>
        public void Respond(int requestId, int picked)
        {
            if (!_open.TryGetValue(requestId, out var request)) return;
            _open.Remove(requestId);

            _world.Log.Append(new ResponseEvent(requestId, picked));

            // Run resolution via the EffectService — gets us a proper
            // scope without us re-implementing the scope plumbing. The
            // resolution itself doesn't emit a wrapper block (it's
            // dispatched via a thin RunResolution path on EffectService).
            _effects.RunResolution(scope => request.Resolve(picked, scope));
        }
    }
}
