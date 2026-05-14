#nullable enable
using System.Collections.Generic;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Tags;

namespace VerticalSlice.Engine.Commands
{
    /// <summary>
    /// What a <see cref="Command"/> sees inside its <see cref="Command.Apply"/>
    /// body. Read access plus the only path from <see cref="IEntity"/>
    /// (carried in the command's payload) to the mutable <see cref="Entity"/>
    /// that can be written to.
    ///
    /// <b>No Execute, no Run, no Subscribe</b> — a command cannot dispatch
    /// other commands or run effects. If a command "needs" to fan out, it's
    /// actually two commands wrapped in an effect.
    /// </summary>
    public interface ICommandScope
    {
        Entity? Resolve(IEntity entity);
        Entity? Get(int entityId);
        IEnumerable<Entity> All();
        IEnumerable<Entity> Where(TagKey<bool> marker);
    }
}
