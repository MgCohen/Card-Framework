#nullable enable
using System.Collections.Generic;
using VerticalSlice.Engine.Stream;

namespace VerticalSlice.Engine.Effects
{
    /// <summary>
    /// A request for a player decision. Distinct from <see cref="Effect"/>
    /// and <see cref="VerticalSlice.Engine.Commands.Command"/> — it's
    /// dispatched by <see cref="VerticalSlice.Engine.Pipeline.RequestsService"/>,
    /// opens its own <see cref="BlockKind.Request"/> block on the wire,
    /// and declares its options as typed <see cref="OptionEvent"/>
    /// children inside that block.
    ///
    /// Per the design pass: options are <i>typed wire events</i> (e.g.,
    /// <c>PickOptionEvent</c>, <c>CancelOptionEvent</c>) — the wire form
    /// is pure data (refs + value), no labels, no funcs. The server-side
    /// mapping from picked-index → action lives in <see cref="Resolve"/>.
    /// </summary>
    public abstract class Request
    {
        public int Id { get; internal set; }
        public abstract string Name { get; }
        public abstract IReadOnlyList<OptionEvent> Options { get; }

        /// <summary>
        /// Server-side resolution. Invoked by the requests service when
        /// a Response arrives. The implementation looks at the picked
        /// index (and the option at that index — useful for checking
        /// <c>CancelOptionEvent</c> short-circuit) and dispatches the
        /// appropriate command / sub-effect via the scope.
        /// </summary>
        public abstract void Resolve(int picked, IEffectScope scope);
    }
}
