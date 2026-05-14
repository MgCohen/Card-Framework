#nullable enable
using System;
using UnityEngine;
using VerticalSlice.Engine.Entities;
using VerticalSlice.Engine.Pipeline;
using VerticalSlice.Engine.Stream;
using VerticalSlice.Mirror;
using VerticalSlice.Sample;

namespace VerticalSlice.UnityScene
{
    /// <summary>
    /// Slice bootstrap. Drop on any GameObject in a scene and Play. Three
    /// IMGUI panels render: Primary state (engine), Mirror state (rebuilt
    /// from the wire stream), Event log. The slider scrubs the mirror
    /// through the log without touching the primary.
    ///
    /// Wires the new shape: <c>GameEngine</c> with <c>Effects</c> and
    /// <c>Commands</c> services, hooks subscribed via <c>Commands.Subscribe</c>,
    /// effects fired via <c>Effects.Run</c>. No more entity subclasses —
    /// caster/target are plain <see cref="Entity"/> with marker tags.
    /// </summary>
    public class SliceBootstrap : MonoBehaviour
    {
        private GameEngine _engine = null!;
        private MirrorWorld _mirror = null!;
        private ScrubController _scrub = null!;

        private Entity? _caster;
        private Entity? _target;

        private Vector2 _eventLogScroll;
        private Vector2 _primaryScroll;
        private Vector2 _mirrorScroll;

        private int _sliderPos;
        private bool _sliderTracksHead = true;

        private void Awake() => Setup();

        private void Setup()
        {
            _engine = new GameEngine();
            DivineShieldRule.Register(_engine.Commands);
            ArmorRule.Register(_engine.Commands);
            BacklashRule.Register(_engine.Commands);

            // No more "OpenSystemBlock" wrapper — bare TagSet/TagChange
            // events at the top of the log are fine. Setup is just a flat
            // sequence of EntityCreated + TagSet events; no enclosing scope.
            _caster = _engine.World.Create();
            _caster.MarkAsCard();
            _caster.SetHp(30);
            _caster.SetMaxHp(30);
            _caster.SetAttack(2);

            _target = _engine.World.Create();
            _target.MarkAsCard();
            _target.SetHp(30);
            _target.SetMaxHp(30);
            _target.SetAttack(0);
            _target.SetArmor(0);

            _mirror = new MirrorWorld();
            _scrub = new ScrubController(_mirror, _engine.World.Log.Events);

            _sliderTracksHead = true;
            _sliderPos = _engine.World.Log.Count;
            _scrub.GoTo(_sliderPos);
        }

        private void AfterCommandIssued()
        {
            _sliderTracksHead = true;
            _sliderPos = _engine.World.Log.Count;
            _scrub.GoTo(_sliderPos);
        }

        private void OnGUI()
        {
            GUILayout.BeginVertical();

            // --- Controls ----------------------------------------------------
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Reset", GUILayout.Width(80))) Setup();

            if (GUILayout.Button("Cast Fireball", GUILayout.Width(120))
                && _caster != null && _target != null)
            {
                _engine.Effects.Run(new Fireball(_caster, _target));
                AfterCommandIssued();
            }

            if (GUILayout.Button("Cast Healing Potion", GUILayout.Width(150))
                && _caster != null && _target != null)
            {
                _engine.Effects.Run(new HealingPotion(_caster, _target));
                AfterCommandIssued();
            }

            if (GUILayout.Button("Target HP = 25", GUILayout.Width(120)) && _target != null)
            {
                _target.SetHp(25);
                AfterCommandIssued();
            }

            GUILayout.EndHorizontal();

            // --- Target toggles + tunables ----------------------------------
            if (_target != null)
            {
                GUILayout.BeginHorizontal();

                var ds = _target.DivineShield();
                var newDs = GUILayout.Toggle(ds, "Divine Shield on Target");
                if (newDs != ds)
                {
                    _target.SetDivineShield(newDs);
                    AfterCommandIssued();
                }

                GUILayout.Space(20);
                var armor = _target.Armor();
                GUILayout.Label($"Target Armor: {armor}", GUILayout.Width(120));
                if (GUILayout.Button("-", GUILayout.Width(30)) && armor > 0)
                {
                    _target.SetArmor(armor - 1);
                    AfterCommandIssued();
                }
                if (GUILayout.Button("+", GUILayout.Width(30)))
                {
                    _target.SetArmor(armor + 1);
                    AfterCommandIssued();
                }

                GUILayout.EndHorizontal();
            }

            // --- Caster toggles (trigger demo) ------------------------------
            if (_caster != null)
            {
                GUILayout.BeginHorizontal();
                var bl = _caster.Backlash();
                var newBl = GUILayout.Toggle(bl, "Backlash on Caster (1 dmg back when it deals damage)");
                if (newBl != bl)
                {
                    _caster.SetBacklash(newBl);
                    AfterCommandIssued();
                }
                GUILayout.EndHorizontal();
            }

            // --- Scrub slider -----------------------------------------------
            GUILayout.Space(8);
            GUILayout.Label($"Scrub: event {_sliderPos} of {_engine.World.Log.Count}" +
                            (_sliderTracksHead ? "  (live)" : "  (rewound)"));
            var newPos = Mathf.RoundToInt(
                GUILayout.HorizontalSlider(_sliderPos, 0f, _engine.World.Log.Count));
            if (newPos != _sliderPos)
            {
                _sliderPos = newPos;
                _sliderTracksHead = _sliderPos == _engine.World.Log.Count;
                _scrub.GoTo(_sliderPos);
            }

            // --- Panels -----------------------------------------------------
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();

            // Primary
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(300));
            GUILayout.Label("PRIMARY STATE (engine)");
            _primaryScroll = GUILayout.BeginScrollView(_primaryScroll, GUILayout.Height(320));
            foreach (var kvp in _engine.World.Entities)
            {
                var ent = kvp.Value;
                GUILayout.Label($"#{ent.Id} {(ent.IsCard() ? "card" : "")}".TrimEnd());
                if (ent.Has(Tags.Hp))           GUILayout.Label($"  hp           = {ent.Hp()}");
                if (ent.Has(Tags.MaxHp))        GUILayout.Label($"  maxHp        = {ent.MaxHp()}");
                if (ent.Has(Tags.Attack))       GUILayout.Label($"  attack       = {ent.Attack()}");
                if (ent.Has(Tags.Armor))        GUILayout.Label($"  armor        = {ent.Armor()}");
                if (ent.Has(Tags.DivineShield)) GUILayout.Label($"  divineShield = {ent.DivineShield()}");
                if (ent.Has(Tags.Backlash))     GUILayout.Label($"  backlash     = {ent.Backlash()}");
                GUILayout.Space(4);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            // Mirror
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(300));
            GUILayout.Label($"MIRROR STATE (cursor {_scrub.Cursor})");
            _mirrorScroll = GUILayout.BeginScrollView(_mirrorScroll, GUILayout.Height(320));
            foreach (var kvp in _mirror.Entities)
            {
                var ent = kvp.Value;
                GUILayout.Label($"#{ent.Id}");
                foreach (var t in ent.Tags)
                    GUILayout.Label($"  {t.Key,-12} = {Stringify(t.Value)}");
                GUILayout.Space(4);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            // Event log
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(540));
            GUILayout.Label("EVENT LOG");
            _eventLogScroll = GUILayout.BeginScrollView(_eventLogScroll, GUILayout.Height(320));
            RenderEventLog();
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        private static string Stringify(object? v) => v switch
        {
            null => "null",
            bool b => b.ToString(),
            _ => v.ToString() ?? "null",
        };

        // --- Event log rendering -------------------------------------------------

        private void RenderEventLog()
        {
            var log = _engine.World.Log;
            int cursor = _scrub.Cursor;
            var renderedStack = new System.Collections.Generic.Stack<bool>();
            int depth = 0;

            for (int i = 0; i < cursor; i++)
            {
                var ev = log[i];

                if (ev is BlockStartEvent)
                {
                    bool hasAppliedContent = BlockHasAppliedContent(i, cursor);
                    renderedStack.Push(hasAppliedContent);
                    if (hasAppliedContent)
                    {
                        GUILayout.Label($"{i:D3} {new string(' ', depth * 2)}{ev}");
                        depth++;
                    }
                    continue;
                }

                if (ev is BlockEndEvent)
                {
                    bool wasRendered = renderedStack.Count > 0 && renderedStack.Pop();
                    if (wasRendered)
                    {
                        depth = Math.Max(0, depth - 1);
                        GUILayout.Label($"{i:D3} {new string(' ', depth * 2)}{ev}");
                    }
                    continue;
                }

                // Leaf event (TagSet, TagChange, EntityCreated, EntityDestroyed)
                GUILayout.Label($"{i:D3} {new string(' ', depth * 2)}{ev}");
            }
        }

        private bool BlockHasAppliedContent(int startIndex, int cursor)
        {
            var log = _engine.World.Log;
            int depth = 0;
            int scanEnd = Math.Min(log.Count, cursor);
            for (int k = startIndex + 1; k < scanEnd; k++)
            {
                var ev = log[k];
                if (ev is BlockStartEvent) { depth++; continue; }
                if (ev is BlockEndEvent)
                {
                    if (depth == 0) return false;
                    depth--;
                    continue;
                }
                return true;   // any leaf event counts as "applied content"
            }
            return false;
        }
    }
}
