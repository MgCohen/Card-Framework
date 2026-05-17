#nullable enable
namespace VerticalSlice.Engine.Commands
{
    /// <summary>
    /// A primitive write operation: <c>DealDamage</c>, <c>Heal</c>,
    /// <c>MoveCard</c>. Mutates state via its payload entities (resolved
    /// to mutable <c>Entity</c> through <see cref="ICommandScope"/>) — no
    /// command-on-command dispatch from inside <see cref="Apply"/>.
    ///
    /// Commands are the surface that hooks attach to:
    /// <c>commands.Subscribe&lt;DealDamage&gt;(After, Reaction, …)</c>.
    /// Authors do not manage scope open/close — <c>CommandService.Execute</c>
    /// wraps <see cref="Apply"/> in a Block on the wire and fires Before /
    /// After hooks around it.
    ///
    /// Command public properties are walked by reflection at BlockStart
    /// emission time to capture the wire payload — keep them simple
    /// (primitives, IEntity refs, value records).
    /// </summary>
    public abstract class Command
    {
        public abstract void Apply(ICommandScope scope);
    }
}
