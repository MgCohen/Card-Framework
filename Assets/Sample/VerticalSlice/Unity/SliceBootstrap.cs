#nullable enable
using System;
using UnityEngine;
using VerticalSlice.Engine.Pipeline;
using VerticalSlice.Engine.Stream;
using VerticalSlice.Mirror;
using VerticalSlice.Sample;

namespace VerticalSlice.UnityScene
{
    /// <summary>
    /// Slice bootstrap. Attach to any GameObject in a fresh scene and hit
    /// Play. Three IMGUI panels render: Primary state (engine), Mirror
    /// state (rebuilt from the wire stream), Event log. The slider scrubs
    /// the mirror through the log without affecting the primary.
    /// </summary>
    public class SliceBootstrap : MonoBehaviour
    {
        private World _world = null!;
        private MirrorWorld _mirror = null!;
        private ScrubController _scrub = null!;

        private Card? _caster;
        private Card? _target;

        private Vector2 _eventLogScroll;
        private Vector2 _primaryScroll;
        private Vector2 _mirrorScroll;

        // Mirror scrub position. We keep this user-controlled separately
        // from the live engine cursor so dragging the slider doesn't fight
        // new events appended by buttons.
        private int _sliderPos;
        private bool _sliderTracksHead = true;

        private void Awake() => Setup();

        private void Setup()
        {
            _world = new World();
            DivineShieldRule.Register(_world.Hooks);

            _caster = _world.Create<Card>();
            _caster.Hp = 30;
            _caster.MaxHp = 30;
            _caster.Attack = 2;

            _target = _world.Create<Card>();
            _target.Hp = 30;
            _target.MaxHp = 30;
            _target.Attack = 0;

            _mirror = new MirrorWorld();
            _scrub = new ScrubController(_mirror, _world.EventLog.Events);

            _sliderTracksHead = true;
            _sliderPos = _world.EventLog.Count;
            _scrub.GoTo(_sliderPos);
        }

        private void AfterCommandIssued()
        {
            // Snap slider to head — the user is interacting with the live
            // game. They can drag it back to inspect history.
            _sliderTracksHead = true;
            _sliderPos = _world.EventLog.Count;
            _scrub.GoTo(_sliderPos);
        }

        private void OnGUI()
        {
            GUILayout.BeginVertical();

            // --- Controls -----------------------------------------------------
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Reset", GUILayout.Width(80))) Setup();

            if (GUILayout.Button("Cast Fireball", GUILayout.Width(140))
                && _caster != null && _target != null)
            {
                Fireball.Cast(_world, _caster, _target);
                AfterCommandIssued();
            }

            if (GUILayout.Button("Target HP = 25", GUILayout.Width(140)) && _target != null)
            {
                _target.Hp = 25;
                AfterCommandIssued();
            }

            if (_target != null)
            {
                var ds = _target.DivineShield;
                var newDs = GUILayout.Toggle(ds, "Divine Shield on Target");
                if (newDs != ds)
                {
                    _target.DivineShield = newDs;
                    AfterCommandIssued();
                }
            }

            GUILayout.EndHorizontal();

            // --- Scrub slider -------------------------------------------------
            GUILayout.Space(8);
            GUILayout.Label($"Scrub: event {_sliderPos} of {_world.EventLog.Count}" +
                            (_sliderTracksHead ? "  (live)" : "  (rewound)"));
            var newPos = Mathf.RoundToInt(
                GUILayout.HorizontalSlider(_sliderPos, 0f, _world.EventLog.Count));
            if (newPos != _sliderPos)
            {
                _sliderPos = newPos;
                _sliderTracksHead = _sliderPos == _world.EventLog.Count;
                _scrub.GoTo(_sliderPos);
            }

            // --- Panels -------------------------------------------------------
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();

            // Primary
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(280));
            GUILayout.Label("PRIMARY STATE (engine)");
            _primaryScroll = GUILayout.BeginScrollView(_primaryScroll, GUILayout.Height(320));
            foreach (var kvp in _world.Entities)
            {
                var ent = kvp.Value;
                GUILayout.Label($"#{ent.Id} {ent.GetType().Name}");
                if (ent is Card card)
                {
                    GUILayout.Label($"  hp           = {card.Hp}");
                    GUILayout.Label($"  maxHp        = {card.MaxHp}");
                    GUILayout.Label($"  attack       = {card.Attack}");
                    GUILayout.Label($"  divineShield = {card.DivineShield}");
                }
                GUILayout.Space(4);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            // Mirror
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(280));
            GUILayout.Label($"MIRROR STATE (cursor {_scrub.Cursor})");
            _mirrorScroll = GUILayout.BeginScrollView(_mirrorScroll, GUILayout.Height(320));
            foreach (var kvp in _mirror.Entities)
            {
                var ent = kvp.Value;
                GUILayout.Label($"#{ent.Id} {ent.TypeName}");
                foreach (var t in ent.Tags)
                    GUILayout.Label($"  {t.Key,-12} = {Stringify(t.Value)}");
                GUILayout.Space(4);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            // Event log — only render events 0..cursor. A block whose body
            // hasn't been re-applied yet (e.g. cursor sits between BlockStart
            // and the first TagChange inside it) is hidden entirely, so its
            // BlockStart and matching BlockEnd both disappear together. As the
            // cursor advances past the first content event, the BlockStart
            // pops back into view.
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(520));
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

        // --- Event log rendering ---------------------------------------------

        private void RenderEventLog()
        {
            var log = _world.EventLog;
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

                // TagChange / EntityCreated
                GUILayout.Label($"{i:D3} {new string(' ', depth * 2)}{ev}");
            }
        }

        /// <summary>
        /// Returns true if at least one TagChange or EntityCreated event
        /// between the BlockStart at <paramref name="startIndex"/> and its
        /// matching BlockEnd has been applied (index &lt; <paramref name="cursor"/>).
        /// Nested BlockStart/BlockEnd pairs are skipped — only direct or
        /// transitive content events count, so a chain of empty nested blocks
        /// still collapses away together.
        /// </summary>
        private bool BlockHasAppliedContent(int startIndex, int cursor)
        {
            var log = _world.EventLog;
            int depth = 0;
            int scanEnd = Math.Min(log.Count, cursor);
            for (int k = startIndex + 1; k < scanEnd; k++)
            {
                var ev = log[k];
                if (ev is BlockStartEvent) { depth++; continue; }
                if (ev is BlockEndEvent)
                {
                    if (depth == 0) return false;   // hit our own end
                    depth--;
                    continue;
                }
                return true;   // TagChange or EntityCreated inside the block
            }
            return false;
        }
    }
}
