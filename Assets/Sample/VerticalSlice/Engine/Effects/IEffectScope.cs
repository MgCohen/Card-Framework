#nullable enable
using System.Collections.Generic;
using VerticalSlice.Engine.Commands;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Engine.Effects
{
    /// <summary>
    /// What an <see cref="Effect"/> sees inside its <see cref="Effect.Run"/>
    /// body. Read-only access to entities (<see cref="IEntity"/>); the only
    /// mutation path is <see cref="Execute"/>, which dispatches a command
    /// that opens its own block and goes through Before/Apply/After hooks.
    ///
    /// <b>No Run, no entity.Set, no Subscribe</b> — the surface is exactly
    /// "issue commands and read state for branching." Effects compose other
    /// effects via the trigger system, not through this scope.
    /// </summary>
    public interface IEffectScope
    {
        void Execute(Command command);

        IEntity? Get(int entityId);
        IEnumerable<IEntity> All();
        IEnumerable<IEntity> Where(TagKey<bool> marker);
    }
}
