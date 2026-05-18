#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;
using StateMachine.Engine;
using StateMachine.Sample;

namespace StateMachine.UnityScene
{
    /// <summary>
    /// IMGUI sandbox for the state-machine spike. Drop on a GameObject and
    /// press Play.
    ///
    /// <para>Three things to watch while clicking through a turn:</para>
    /// <list type="bullet">
    /// <item><b>Allowed actions</b> mirror the current <c>GameState.Actions</c>
    /// list. Pass through Upkeep / Main1 / DeclareAttackers / DeclareBlockers /
    /// CombatDamage / Main2 / End; the buttons change as the state changes.</item>
    /// <item><b>State transitions</b> happen through commands, not a parallel
    /// path — every transition lands as a <c>StatePushEvent</c> in the wire
    /// log, sandwiched between the OnExit + OnEnter generated commands.</item>
    /// <item><b>Stateless states</b>: during DeclareAttackers, toggling
    /// attackers writes the <c>IsAttacking</c> tag on entities; the state
    /// record carries zero data. CombatDamage then reads attackers off the
    /// world to generate damage commands.</item>
    /// </list>
    ///
    /// <para>Seed: two creatures per player. Combat works as long as the
    /// active player declares at least one attacker — CombatDamage's
    /// completion is driven by per-attacker <c>DealDamageCommand</c>s, so
    /// committing with zero attackers leaves the state machine parked in
    /// CombatDamage (a spike-known edge; the demo guides past it).</para>
    /// </summary>
    public sealed class StateMachineBootstrap : MonoBehaviour
    {
        private GameEngine   _engine = null!;
        private StateService _state  = null!;
        private int _appliedLogCount;   // for autoscroll
        private Vector2 _leftScroll, _logScroll;

        private void Awake() => Setup();

        private void Setup()
        {
            var built = MtgBootstrap.Build();
            _engine = built.engine;
            _state  = built.state;
            _appliedLogCount = 0;

            // Two creatures per player so DeclareAttackers / CombatDamage have
            // something to operate on. Power/toughness chosen so they're
            // distinguishable in the readout.
            MtgBootstrap.SpawnCreature(_engine.World, PlayerId.Player1, power: 3, toughness: 2);
            MtgBootstrap.SpawnCreature(_engine.World, PlayerId.Player1, power: 2, toughness: 1);
            MtgBootstrap.SpawnCreature(_engine.World, PlayerId.Player2, power: 4, toughness: 4);
            MtgBootstrap.SpawnCreature(_engine.World, PlayerId.Player2, power: 1, toughness: 3);
        }

        // ─── IMGUI ─────────────────────────────────────────────────────────

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(8, 8, Screen.width - 16, Screen.height - 16));

            DrawHeader();
            GUILayout.Space(6);

            GUILayout.BeginHorizontal();
            DrawControlPanel();
            DrawLogPanel();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void DrawHeader()
        {
            var m = _engine.World.Match;
            byte kind = _engine.States.TryKindOf(_state.Current, out var k) ? k : (byte)255;

            GUILayout.BeginHorizontal(GUI.skin.box);
            if (GUILayout.Button("Reset", GUILayout.Width(80)))
            {
                Setup();
                GUILayout.EndHorizontal();
                return;
            }
            GUILayout.Space(10);
            GUILayout.Label($"Turn {m.Turn}", GUILayout.Width(70));
            GUILayout.Label($"Active: {m.ActivePlayer}", GUILayout.Width(140));
            GUILayout.Label($"Defending: {m.Defending}", GUILayout.Width(160));
            GUILayout.FlexibleSpace();
            GUILayout.Label($"State: {_state.Current.GetType().Name} (kind {kind})");
            GUILayout.EndHorizontal();
        }

        // ─── Left column: creatures + allowed actions ──────────────────────

        private void DrawControlPanel()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(Screen.width * 0.58f));

            GUILayout.Label("CREATURES");
            _leftScroll = GUILayout.BeginScrollView(_leftScroll, GUILayout.MinHeight(280));
            DrawCreaturesByPlayer(PlayerId.Player1);
            DrawCreaturesByPlayer(PlayerId.Player2);
            GUILayout.EndScrollView();

            GUILayout.Space(6);
            GUILayout.Label("ALLOWED ACTIONS — from current state's Actions list");
            DrawActionButtons();

            GUILayout.EndVertical();
        }

        private void DrawCreaturesByPlayer(PlayerId player)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"— {player} —");

            bool inDeclareAttackers = _state.Current is DeclareAttackersState;
            var activePlayer = _engine.World.Match.ActivePlayer;

            foreach (var e in _engine.World.Entities)
            {
                if (e.Get(MtgTags.Controller) != player) continue;
                if (!e.Has(MtgTags.Power)) continue;   // skip non-creatures

                int power     = e.Get(MtgTags.Power);
                int toughness = e.Get(MtgTags.Toughness);
                bool attacking = e.Has(MtgTags.IsAttacking);
                bool resolved  = e.Has(MtgTags.DamageResolved);

                GUILayout.BeginHorizontal();
                GUILayout.Label($"#{e.Id}", GUILayout.Width(40));
                GUILayout.Label($"{power}/{toughness}", GUILayout.Width(60));
                GUILayout.Label(attacking ? "ATK" : "   ", GUILayout.Width(40));
                GUILayout.Label(resolved  ? "✓ dmg"  : "      ", GUILayout.Width(60));

                // Toggle only meaningful during DeclareAttackers, and only for
                // the active player's creatures (defender doesn't attack).
                bool canToggle = inDeclareAttackers && player == activePlayer;
                GUI.enabled = canToggle;
                if (GUILayout.Button(attacking ? "Unselect" : "Attack", GUILayout.Width(90)))
                    _engine.Enqueue(new ToggleAttackerCommand(e.Id));
                GUI.enabled = true;

                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }

        private void DrawActionButtons()
        {
            var actions = _state.Current.Actions;

            // Per-state command authoring: we know the spike's command surface
            // and which ones are no-arg-publishable from this panel. Toggle is
            // surfaced as a per-creature row above.
            GUILayout.BeginHorizontal();
            foreach (var actionType in actions)
            {
                if (actionType == typeof(PassCommand))
                    if (GUILayout.Button("Pass", GUILayout.Width(100)))
                        _engine.Enqueue(new PassCommand());

                else if (actionType == typeof(CommitAttackersCommand))
                    if (GUILayout.Button("Commit Attackers", GUILayout.Width(160)))
                        _engine.Enqueue(new CommitAttackersCommand());

                else if (actionType == typeof(CommitBlockersCommand))
                    if (GUILayout.Button("Commit Blockers", GUILayout.Width(160)))
                        _engine.Enqueue(new CommitBlockersCommand());

                else if (actionType == typeof(ToggleAttackerCommand))
                    GUILayout.Label("(toggle per-creature above)", GUILayout.Width(220));

                else
                    GUILayout.Label($"({actionType.Name})", GUILayout.Width(160));
            }
            if (actions.Count == 0)
                GUILayout.Label("— no player input; state advances when its " +
                                "completion predicate fires —");
            GUILayout.EndHorizontal();

            DrawCombatDamageHint();
        }

        private void DrawCombatDamageHint()
        {
            if (_state.Current is not CombatDamageState) return;

            // CombatDamage's IsCompleted predicate fires after each
            // DealDamageCommand; with zero attackers no commands are emitted
            // and the state machine has no event to react to. Spike-known
            // limitation — surface it so the demo doesn't look broken.
            int attackerCount = 0;
            foreach (var _ in _engine.World.With(MtgTags.IsAttacking)) attackerCount++;
            if (attackerCount == 0)
                GUILayout.Label("CombatDamage with 0 attackers — Reset and " +
                                "declare at least one attacker next turn.");
        }

        // ─── Right column: wire log ────────────────────────────────────────

        private void DrawLogPanel()
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(Screen.width * 0.38f));
            GUILayout.Label($"WIRE LOG — {_engine.World.Log.Count} events");

            _logScroll = GUILayout.BeginScrollView(_logScroll);
            int i = 0;
            foreach (var ev in _engine.World.Log)
            {
                GUILayout.Label($"{i:00}  {Format(ev)}");
                i++;
            }
            GUILayout.EndScrollView();

            // Autoscroll when new events land.
            if (_engine.World.Log.Count != _appliedLogCount)
            {
                _logScroll.y = float.MaxValue;
                _appliedLogCount = _engine.World.Log.Count;
            }

            GUILayout.EndVertical();
        }

        private string Format(WireEvent ev) => ev switch
        {
            StatePushEvent sp        => $"StatePush(prev={KindLabel(sp.PrevKind)}, new={KindLabel(sp.NewKind)})",
            CommandExecutedEvent ce  => $"Command({ce.CommandName})",
            _                        => ev.ToString() ?? ev.GetType().Name,
        };

        private string KindLabel(byte kind)
        {
            foreach (var (k, t) in _engine.States.All)
                if (k == kind) return $"{kind}:{t.Name}";
            return kind.ToString();
        }
    }
}
