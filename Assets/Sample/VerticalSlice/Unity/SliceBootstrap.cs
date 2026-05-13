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

            // Event log
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(520));
            GUILayout.Label("EVENT LOG");
            _eventLogScroll = GUILayout.BeginScrollView(_eventLogScroll, GUILayout.Height(320));
            int depth = 0;
            for (int i = 0; i < _world.EventLog.Count; i++)
            {
                var ev = _world.EventLog[i];
                if (ev is BlockEndEvent) depth = Math.Max(0, depth - 1);

                string indent = new(' ', depth * 2);
                string marker = i < _scrub.Cursor ? "▶" : "·";
                GUILayout.Label($"{marker} {i:D3} {indent}{ev}");

                if (ev is BlockStartEvent) depth++;
            }
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
    }
}
