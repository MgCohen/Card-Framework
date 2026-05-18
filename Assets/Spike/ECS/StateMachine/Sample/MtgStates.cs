#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using StateMachine.Engine;

namespace StateMachine.Sample
{
    /// <summary>
    /// All state records carry ZERO data fields. Identity = type. The MTG
    /// flow goes Upkeep → Main1 → DeclareAttackers → DeclareBlockers →
    /// CombatDamage → Main2 → End → (Upkeep next turn).
    ///
    /// <para>Three reads are interesting to compare:
    /// <list type="bullet">
    ///   <item>UpkeepState.OnEnter — static list, demonstrates the generator pattern.</item>
    ///   <item>DeclareAttackersState.IsCompleted — predicate, no entity read.</item>
    ///   <item>CombatDamageState.OnEnter + IsCompleted — both read entities.
    ///         This is the §v6 stateless-state PROOF: selected attackers live
    ///         on the entities, not on the record.</item>
    /// </list></para>
    /// </summary>
    public sealed record UpkeepState : GameState
    {
        public override IReadOnlyList<Type> Actions => new[] { typeof(PassCommand) };

        public override IReadOnlyList<GameAction> OnEnter(IStateScope s) => new GameAction[]
        {
            new UntapAllCommand(),
            new DrawCardCommand(s.World.Match.ActivePlayer)
        };

        public override bool IsCompleted(GameAction a, CommandResult r, IStateScope s) =>
            a is PassCommand;
    }

    public sealed record Main1State : GameState
    {
        public override IReadOnlyList<Type> Actions => new[] { typeof(PassCommand) };

        public override bool IsCompleted(GameAction a, CommandResult r, IStateScope s) =>
            a is PassCommand;
    }

    public sealed record DeclareAttackersState : GameState
    {
        public override IReadOnlyList<Type> Actions => new[]
        {
            typeof(ToggleAttackerCommand),
            typeof(CommitAttackersCommand)
        };

        public override bool IsCompleted(GameAction a, CommandResult r, IStateScope s) =>
            a is CommitAttackersCommand;
    }

    public sealed record DeclareBlockersState : GameState
    {
        public override IReadOnlyList<Type> Actions => new[] { typeof(CommitBlockersCommand) };

        public override bool IsCompleted(GameAction a, CommandResult r, IStateScope s) =>
            a is CommitBlockersCommand;
    }

    public sealed record CombatDamageState : GameState
    {
        // No actions — pure resolution, no player input.
        public override IReadOnlyList<Type> Actions => Array.Empty<Type>();

        /// <summary>
        /// THE proof. "Which creatures are attacking?" is a query on the world,
        /// not a field on the record. If you re-pushed this state, you'd get
        /// the same answer — the answer lives on the entities.
        /// </summary>
        public override IReadOnlyList<GameAction> OnEnter(IStateScope s)
        {
            var attackers = s.World.With(MtgTags.IsAttacking).ToList();
            var cmds = new List<GameAction>(attackers.Count);
            foreach (var atk in attackers)
                cmds.Add(new DealDamageCommand(atk.Id, atk.Get(MtgTags.Power)));
            return cmds;
        }

        public override bool IsCompleted(GameAction a, CommandResult r, IStateScope s)
        {
            // All attackers have resolved damage → done. Reads entities again.
            foreach (var atk in s.World.With(MtgTags.IsAttacking))
                if (!atk.Has(MtgTags.DamageResolved)) return false;
            return true;
        }
    }

    public sealed record Main2State : GameState
    {
        public override IReadOnlyList<Type> Actions => new[] { typeof(PassCommand) };

        public override bool IsCompleted(GameAction a, CommandResult r, IStateScope s) =>
            a is PassCommand;
    }

    public sealed record EndState : GameState
    {
        public override IReadOnlyList<Type> Actions => new[] { typeof(PassCommand) };

        public override IReadOnlyList<GameAction> OnEnter(IStateScope s) => new GameAction[]
        {
            new ClearTagCommand("IsAttacking"),
            new ClearTagCommand("Blocking"),
            new ClearTagCommand("DamageResolved"),
            new IncrementTurnCommand(),
            new SwapActivePlayerCommand()
        };

        public override bool IsCompleted(GameAction a, CommandResult r, IStateScope s) =>
            a is PassCommand;
    }

    /// <summary>
    /// Framework baselines from spike §Q7 — kinds 0, 1, 2 reserved.
    /// </summary>
    public sealed record MatchNotStartedState : GameState
    {
        public override IReadOnlyList<Type> Actions => new[] { typeof(PassCommand) };

        public override bool IsCompleted(GameAction a, CommandResult r, IStateScope s) =>
            a is PassCommand;
    }

    public sealed record PlayingState         : GameState;
    public sealed record MatchEndedState      : GameState;
}
