#nullable enable
using System;
using System.Collections.Generic;

namespace StateMachine.Engine
{
    public interface ICommandHandler<in TCmd, out TRes>
        where TCmd : GameAction
        where TRes : CommandResult
    {
        TRes Handle(TCmd cmd, ICommandScope scope);
    }

    /// <summary>
    /// Type-erased adapter so the pipeline can hold all handlers in one dict
    /// without making the pipeline itself generic.
    /// </summary>
    internal interface IHandlerInvoker
    {
        CommandResult Invoke(GameAction cmd, ICommandScope scope);
    }

    internal sealed class HandlerInvoker<TCmd, TRes> : IHandlerInvoker
        where TCmd : GameAction
        where TRes : CommandResult
    {
        private readonly ICommandHandler<TCmd, TRes> _inner;
        public HandlerInvoker(ICommandHandler<TCmd, TRes> inner) { _inner = inner; }
        public CommandResult Invoke(GameAction cmd, ICommandScope scope) =>
            _inner.Handle((TCmd)cmd, scope);
    }

    public sealed class HandlerRegistry
    {
        private readonly Dictionary<Type, IHandlerInvoker> _byType = new();

        public void Register<TCmd, TRes>(ICommandHandler<TCmd, TRes> handler)
            where TCmd : GameAction
            where TRes : CommandResult
        {
            _byType[typeof(TCmd)] = new HandlerInvoker<TCmd, TRes>(handler);
        }

        internal IHandlerInvoker Resolve(GameAction cmd)
        {
            if (!_byType.TryGetValue(cmd.GetType(), out var invoker))
                throw new InvalidOperationException(
                    $"No handler registered for {cmd.GetType().Name}.");
            return invoker;
        }
    }
}
