#nullable enable
using System;
using System.Collections.Generic;

namespace StateMachine.Engine
{
    /// <summary>
    /// Spike §v6: zero-data record. Identity is the type. Behavior only.
    /// All "would-be state data" lives on entities or the Match singleton.
    /// </summary>
    public abstract record GameState
    {
        /// <summary>Command types this state accepts as player input.</summary>
        public virtual IReadOnlyList<Type> Actions => Array.Empty<Type>();

        /// <summary>Hook outputs fired when the state is entered. Generators —
        /// the returned commands are enqueued by the push handler; they
        /// themselves land as wire events. Replay reads events, never re-fires.</summary>
        public virtual IReadOnlyList<GameAction> OnEnter(IStateScope scope) =>
            Array.Empty<GameAction>();

        /// <summary>Hook outputs fired when leaving the state.</summary>
        public virtual IReadOnlyList<GameAction> OnExit(IStateScope scope) =>
            Array.Empty<GameAction>();

        /// <summary>Did this command (and its result) cause the state to finish?
        /// Engine asks the flow for a successor when this returns true.</summary>
        public virtual bool IsCompleted(GameAction action, CommandResult result, IStateScope scope) =>
            false;
    }
}
