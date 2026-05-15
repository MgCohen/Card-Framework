#nullable enable
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Pipeline;
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Engine.Commands
{
    /// <summary>
    /// What a <see cref="Command"/> sees inside its <see cref="Command.Apply"/>
    /// body. Read access via <see cref="Get"/> / <see cref="Resolve"/> /
    /// indexed queries (<see cref="With"/> / <see cref="Where{T}"/>), the
    /// path from <see cref="IEntity"/> to mutable <see cref="Entity"/> for
    /// writes, and the two engine surfaces a command can legitimately
    /// reach: the <see cref="Stack"/> (to push effects for later
    /// resolution) and the <see cref="EffectStore"/> (to look up an
    /// entity-attached effect via <c>BuildDirect</c> — used by
    /// play-style commands).
    ///
    /// <b>No Execute, no Run, no Subscribe</b> — a command cannot dispatch
    /// other commands or directly run effects. It can push an effect onto
    /// the stack (e.g. <c>PlayCard.Apply</c> looks up the card's OnCast
    /// factory and pushes the resulting effect); the engine resolves it
    /// on its own schedule.
    ///
    /// <para><b>Query path.</b> <see cref="With"/> for marker queries
    /// (<c>scope.With(Tags.IsMinion)</c>); <see cref="Where{T}"/> for
    /// value-equals queries (<c>scope.Where(Tags.Zone, p1Board.Id)</c>).
    /// Chain via <c>.And(...)</c>. Returns an <see cref="EntityQuery"/>
    /// (struct enumerable, zero-alloc iteration). Foreach yields
    /// <see cref="IEntity"/>; upgrade to mutable <see cref="Entity"/> via
    /// <see cref="Resolve"/> on a specific result if you need to write.</para>
    /// </summary>
    public interface ICommandScope
    {
        Entity? Resolve(IEntity entity);
        Entity? Get(int entityId);

        EntityQuery With(TagKey<bool> marker);
        EntityQuery Where<T>(TagKey<T> key, T value) where T : notnull;

        /// <summary>
        /// The unified store of hook subscriptions and direct-trigger
        /// factories. Commands typically only read from it
        /// (<c>BuildDirect</c>) — they don't register new hooks
        /// mid-Apply.
        /// </summary>
        EffectStore EffectStore { get; }

        /// <summary>
        /// The engine's effect stack. Commands push onto it; the engine's
        /// stack-resolution policy decides when to pop and resolve.
        /// </summary>
        Stack Stack { get; }
    }
}
