#nullable enable
using System.Collections.Generic;
using Scaffold.Variables;
using UnityEngine;

namespace Scaffold.GraphFlow.CardSandbox.Showcase
{
    public sealed class VariableShowcase : MonoBehaviour
    {
        [SerializeField] CardEffectGraphAsset? graphAsset;

        CardEffectRunner _runner = null!;
        EventBus _bus = null!;
        DamageSink _damage = null!;

        IVariableHandle<int>? _hp;
        IVariableHandle<int>? _attack;
        IVariableHandle<string>? _name;

        readonly List<string> _log = new();
        Vector2 _logScroll;
        int _totalDamage;

        void Start()
        {
            _bus = new EventBus();
            _damage = new DamageSink();

            var builder = new CardEffectBuilder(_bus, _damage);
            var asset = graphAsset != null ? graphAsset : StrikeWithVariables.BuildAsset();
            _runner = builder.Build(asset);

            _runner.Variables.TryGet<int>("hp", out _hp);
            _runner.Variables.TryGet<int>("attack", out _attack);
            _runner.Variables.TryGet<string>("name", out _name);

            _hp?.Subscribe(v => AddLog($"HP changed → {v}"));
            _attack?.Subscribe(v => AddLog($"Attack changed → {v}"));
            _name?.Subscribe(v => AddLog($"Name changed → \"{v}\""));

            AddLog("Runner built. Variables seeded from defaults.");
        }

        async void PlayCard()
        {
            AddLog("--- Playing card ---");
            await _runner.Run(new OnPlay());
            _totalDamage += _damage.LastAmount;
            AddLog($"Dealt {_damage.LastAmount} damage (total: {_totalDamage})");
        }

        void AddLog(string msg)
        {
            _log.Add($"[{Time.frameCount}] {msg}");
            if (_log.Count > 200) _log.RemoveAt(0);
            _logScroll.y = float.MaxValue;
        }

        void OnGUI()
        {
            var w = Screen.width;
            var area = new Rect(20, 20, Mathf.Min(w - 40, 500), Screen.height - 40);
            GUILayout.BeginArea(area);

            GUILayout.Label("<size=20><b>GraphFlow — Variable Showcase</b></size>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.Space(10);

            DrawVariablePanel();
            GUILayout.Space(10);

            DrawActions();
            GUILayout.Space(10);

            DrawLog();

            GUILayout.EndArea();
        }

        void DrawVariablePanel()
        {
            GUILayout.Label("<b>Blackboard Variables</b>", new GUIStyle(GUI.skin.label) { richText = true });

            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.BeginHorizontal();
            GUILayout.Label("HP:", GUILayout.Width(80));
            if (_hp != null)
            {
                var hpStr = GUILayout.TextField(_hp.Value.ToString(), GUILayout.Width(80));
                if (int.TryParse(hpStr, out var hpVal) && hpVal != _hp.Value)
                    _hp.Set(hpVal);
                _hp.Set((int)GUILayout.HorizontalSlider(_hp.Value, 0, 200, GUILayout.Width(150)));
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Attack:", GUILayout.Width(80));
            if (_attack != null)
            {
                var atkStr = GUILayout.TextField(_attack.Value.ToString(), GUILayout.Width(80));
                if (int.TryParse(atkStr, out var atkVal) && atkVal != _attack.Value)
                    _attack.Set(atkVal);
                _attack.Set((int)GUILayout.HorizontalSlider(_attack.Value, 0, 50, GUILayout.Width(150)));
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Name:", GUILayout.Width(80));
            if (_name != null)
            {
                var newName = GUILayout.TextField(_name.Value, GUILayout.Width(150));
                if (newName != _name.Value) _name.Set(newName);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        void DrawActions()
        {
            GUILayout.Label("<b>Actions</b>", new GUIStyle(GUI.skin.label) { richText = true });
            GUILayout.BeginHorizontal();

            if (GUILayout.Button("Play Card", GUILayout.Height(30)))
                PlayCard();

            if (GUILayout.Button("Take 20 Damage", GUILayout.Height(30)))
            {
                if (_hp != null) _hp.Set(_hp.Value - 20);
            }

            if (GUILayout.Button("Heal +30", GUILayout.Height(30)))
            {
                if (_hp != null) _hp.Set(_hp.Value + 30);
            }

            if (GUILayout.Button("Buff Attack +5", GUILayout.Height(30)))
            {
                if (_attack != null) _attack.Set(_attack.Value + 5);
            }

            GUILayout.EndHorizontal();

            GUILayout.Label($"Total damage dealt: {_totalDamage}");
        }

        void DrawLog()
        {
            GUILayout.Label("<b>Event Log</b>", new GUIStyle(GUI.skin.label) { richText = true });
            _logScroll = GUILayout.BeginScrollView(_logScroll, GUI.skin.box, GUILayout.Height(250));
            foreach (var line in _log)
                GUILayout.Label(line);
            GUILayout.EndScrollView();
        }
    }
}
