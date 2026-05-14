#nullable enable
using VerticalSlice.Engine.Commands;

namespace VerticalSlice.Engine.Hooks
{
    /// <summary>
    /// Hook handler signature: receives the in-flight command (typed) and
    /// an <see cref="IHookScope"/>. Replacement-band Before hooks mutate
    /// the cmd payload directly; Reaction-band After hooks typically call
    /// <c>scope.Run(new SomeReaction(...))</c> to fire a follow-up effect.
    /// </summary>
    public delegate void HookHandler<in TCommand>(TCommand command, IHookScope scope)
        where TCommand : Command;
}
