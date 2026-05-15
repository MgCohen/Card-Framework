#nullable enable
using System.Collections.Generic;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Pipeline;
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Engine.Commands
{
    /// <summary>
    /// What a <see cref="Command"/> sees inside its <see cref="Command.Apply"/>
    /// body. Read access, the path from <see cref="IEntity"/> (in the
    /// command's payload) to the mutable <see cref="Entity"/> for writes,
    /// and the two engine surfaces a command can legitimately reach for:
    /// the <see cref="Stack"/> (to push effects for later resolution) and
    /// the <see cref="CardEffects"/> store (to look up an effect attached
    /// to a card — used by play-style commands).
    ///
    /// <b>No Execute, no Run, no Subscribe</b> — a command cannot dispatch
    /// other commands or directly run effects. It can push an effect onto
    /// the stack (e.g., <c>PlayCard.Apply</c> looks up the card's OnCast
    /// effect and pushes it); the engine resolves it on its own schedule.
    /// </summary>
    public interface ICommandScope
    {
        Entity? Resolve(IEntity entity);
        Entity? Get(int entityId);
        IEnumerable<Entity> All();
        IEnumerable<Entity> Where(TagKey<bool> marker);

        /// <summary>
        /// The store of card-attached effect factories (OnCast, Deathrattle,
        /// etc.). Commands look up the right effect for a card here at
        /// play / trigger time.
        /// </summary>
        CardEffectStore CardEffects { get; }

        /// <summary>
        /// The engine's effect stack. Commands push onto it; the engine's
        /// stack-resolution policy decides when to pop and resolve.
        /// </summary>
        Stack Stack { get; }
    }
}
