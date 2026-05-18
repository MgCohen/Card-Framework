#nullable enable
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spike.Modifiers
{
    // Drop on a GameObject in an empty scene and Play. Four worked
    // scenarios from modifiers-as-entities.md run side-by-side; each panel
    // owns a target minion and a button row that walks the canonical
    // attach/hit/detach sequence. The tag bag below each panel updates live.
    //
    // Reset rebuilds the engine against the same catalogs — content
    // survives, match state restarts (same pattern as EntitySetup).
    public sealed class ModifiersBootstrap : MonoBehaviour
    {
        // ─── Catalog ids ─────────────────────────────────────────────────
        public const int CARD_SQUIRE   = 201;
        public const int CARD_RAPTOR   = 203;
        public const int CARD_DEFENDER = 204;

        public const int MOD_HOP        = 901;   // Hand of Protection (DS)
        public const int MOD_ADD3_ATK   = 501;
        public const int MOD_MUL2_ATK   = 502;
        public const int MOD_SET1_ATK   = 503;
        public const int MOD_BLESSED    = 1003;  // Blessed Champion composite
        public const int MOD_IRON       = 1101;  // +5 Defense
        public const int MOD_CONDCHARGE = 1102;  // +3 if Defense>5 else +1

        private Catalogs   _catalogs = null!;
        private GameEngine _engine   = null!;
        private Vector2    _scroll;

        // Scenario state — entities + step indices + logs.
        private sealed class Scenario
        {
            public string  Title  = "";
            public Entity  Target = null!;
            public int     Step;
            public List<string> Log = new();
            public List<(string label, System.Action action)> Steps = new();
        }
        private List<Scenario> _scenarios = new();

        private void Awake()
        {
            _catalogs = BuildCatalogs();
            Rebuild();
        }

        private void Rebuild()
        {
            _engine    = new GameEngine(_catalogs);
            _scenarios = new List<Scenario>
            {
                BuildScenario_TransientGrant(),
                BuildScenario_RebaseableOrdering(),
                BuildScenario_BlessedComposite(),
                BuildScenario_ConditionalDependency(),
            };
        }

        // ─── Catalog ─────────────────────────────────────────────────────

        public static Catalogs BuildCatalogs()
        {
            var c = new Catalogs();

            c.Cards.Add(new CardDefinition(
                CARD_SQUIRE, "Argent Squire", CardKind.Minion,
                Cost: 1, Attack: 1, Health: 1, DivineShield: true));
            c.Cards.Add(new CardDefinition(
                CARD_RAPTOR, "Bloodfen Raptor", CardKind.Minion,
                Cost: 2, Attack: 3, Health: 2));
            c.Cards.Add(new CardDefinition(
                CARD_DEFENDER, "Frontline Defender", CardKind.Minion,
                Cost: 2, Attack: 1, Health: 4, Defense: 2));

            // Hand of Protection — pure transient grant (DivineShield).
            c.Modifiers.Add(new ModifierDefinition(
                MOD_HOP, "Hand of Protection",
                OnAttach: new SetBoolEffect(Tags.DivineShield, true),
                Tooltip:  new ModifierTooltip("Divine Shield")));

            // Multi-mul ordering on Attack.
            c.Modifiers.Add(new ModifierDefinition(
                MOD_ADD3_ATK, "+3 ATK",
                Refresh: new AddIntEffect(Tags.Attack, 3),
                Order:   100,
                Tooltip: new ModifierTooltip("+3 Attack")));
            c.Modifiers.Add(new ModifierDefinition(
                MOD_MUL2_ATK, "x2 ATK",
                Refresh: new MulIntEffect(Tags.Attack, 2),
                Order:   200,
                Tooltip: new ModifierTooltip("x2 Attack")));
            c.Modifiers.Add(new ModifierDefinition(
                MOD_SET1_ATK, "Set ATK 1",
                Refresh: new SetIntEffect(Tags.Attack, 1),
                Order:   300,
                Tooltip: new ModifierTooltip("Set Attack to 1")));

            // Blessed Champion — composite OnAttach (DS + Health bump) +
            // composite Refresh (Attack + MaxHealth).
            c.Modifiers.Add(new ModifierDefinition(
                MOD_BLESSED, "Blessed Champion",
                OnAttach: new CompositeEffect(
                    new SetBoolEffect(Tags.DivineShield, true),
                    new AddIntEffect (Tags.Health,       4)),
                Refresh:  new CompositeEffect(
                    new AddIntEffect(Tags.Attack,    4),
                    new AddIntEffect(Tags.MaxHealth, 4)),
                Order:    150,
                Tooltip:  new ModifierTooltip("+4/+4, Divine Shield")));

            // Conditional dependency chain.
            c.Modifiers.Add(new ModifierDefinition(
                MOD_IRON, "Iron Plating",
                Refresh: new AddIntEffect(Tags.Defense, 5),
                Order:   100,
                Tooltip: new ModifierTooltip("+5 Defense")));
            c.Modifiers.Add(new ModifierDefinition(
                MOD_CONDCHARGE, "Conditional Charge",
                Refresh: new ConditionalEffect(
                    cond:  e => e.Get(Tags.Defense) > 5,
                    thenE: new AddIntEffect(Tags.Attack, 3),
                    elseE: new AddIntEffect(Tags.Attack, 1)),
                Order:   200,
                Tooltip: new ModifierTooltip("+3 ATK if Defense>5 else +1")));

            return c;
        }

        // ─── Scenarios ───────────────────────────────────────────────────
        //
        // Each scenario builds its target up front and queues a step list.
        // The IMGUI surface drives Steps[step].action() and logs the
        // result. Validating each scenario is "press the buttons in order
        // and watch the tag bag".

        private Scenario BuildScenario_TransientGrant()
        {
            var s = new Scenario { Title = "Transient grant — two HoPs, one hit" };
            var raptor = _engine.SpawnCard(_catalogs.Cards[CARD_RAPTOR]);
            s.Target = raptor;
            s.Log.Add($"Base: Atk={raptor.Get(Tags.Attack)}, HP={raptor.Get(Tags.Health)}/{raptor.Get(Tags.MaxHealth)}, DS={raptor.Get(Tags.DivineShield)}");

            Entity? hopA = null, hopB = null;
            s.Steps.Add(("Attach HoP_A", () =>
            {
                hopA = _engine.SpawnModifier(_catalogs.Modifiers[MOD_HOP], raptor);
                s.Log.Add("Attach HoP_A → OnAttach Set(DS,true).");
            }));
            s.Steps.Add(("Attach HoP_B", () =>
            {
                hopB = _engine.SpawnModifier(_catalogs.Modifiers[MOD_HOP], raptor);
                s.Log.Add("Attach HoP_B → OnAttach Set(DS,true) idempotent.");
            }));
            s.Steps.Add(("Take hit (consume DS)", () =>
            {
                raptor.Set(Tags.DivineShield, false);
                s.Log.Add("Hit → gameplay Set(DS,false). Damage skipped.");
            }));
            s.Steps.Add(("Detach HoP_A", () =>
            {
                if (hopA != null) _engine.DestroyModifier(hopA);
                s.Log.Add("Detach HoP_A → refresh; transient DS untouched (still false). ✓");
            }));
            s.Steps.Add(("Detach HoP_B", () =>
            {
                if (hopB != null) _engine.DestroyModifier(hopB);
                s.Log.Add("Detach HoP_B → refresh; DS still false. ✓");
            }));
            s.Steps.Add(("Attach HoP_C", () =>
            {
                _engine.SpawnModifier(_catalogs.Modifiers[MOD_HOP], raptor);
                s.Log.Add("Attach HoP_C → DS=true again. ✓");
            }));
            return s;
        }

        private Scenario BuildScenario_RebaseableOrdering()
        {
            var s = new Scenario { Title = "Rebaseable ordering — +3 / x2 / Set1 on Attack" };
            var sq = _engine.SpawnCard(_catalogs.Cards[CARD_SQUIRE]);
            s.Target = sq;
            s.Log.Add($"Base: Atk={sq.Get(Tags.Attack)}.");

            Entity? add = null, mul = null, set = null;
            s.Steps.Add(("Attach +3 ATK", () =>
            {
                add = _engine.SpawnModifier(_catalogs.Modifiers[MOD_ADD3_ATK], sq);
                s.Log.Add($"Refresh: Atk = 1 + 3 = 4. Got {sq.Get(Tags.Attack)}.");
            }));
            s.Steps.Add(("Attach x2 ATK", () =>
            {
                mul = _engine.SpawnModifier(_catalogs.Modifiers[MOD_MUL2_ATK], sq);
                s.Log.Add($"Refresh: Atk = (1+3)*2 = 8. Got {sq.Get(Tags.Attack)}.");
            }));
            s.Steps.Add(("Attach Set ATK 1", () =>
            {
                set = _engine.SpawnModifier(_catalogs.Modifiers[MOD_SET1_ATK], sq);
                s.Log.Add($"Refresh: Atk = set(1) → 1. Got {sq.Get(Tags.Attack)}.");
            }));
            s.Steps.Add(("Detach Set", () =>
            {
                if (set != null) _engine.DestroyModifier(set);
                s.Log.Add($"Refresh: Atk = (1+3)*2 = 8. Got {sq.Get(Tags.Attack)}.");
            }));
            s.Steps.Add(("Detach +3", () =>
            {
                if (add != null) _engine.DestroyModifier(add);
                s.Log.Add($"Refresh: Atk = 1*2 = 2. Got {sq.Get(Tags.Attack)}. ✓ (stateful undo would give 5)");
            }));
            s.Steps.Add(("Detach x2", () =>
            {
                if (mul != null) _engine.DestroyModifier(mul);
                s.Log.Add($"Refresh: Atk = 1. Got {sq.Get(Tags.Attack)}.");
            }));
            return s;
        }

        private Scenario BuildScenario_BlessedComposite()
        {
            var s = new Scenario { Title = "Composite — Blessed Champion on Squire" };
            var sq = _engine.SpawnCard(_catalogs.Cards[CARD_SQUIRE]);
            s.Target = sq;
            s.Log.Add($"Squire 1/1, DS=true (intrinsic), HP={sq.Get(Tags.Health)}/{sq.Get(Tags.MaxHealth)}.");
            // The Squire has intrinsic DS already — but the scenario in the
            // doc uses a vanilla 1/1 base. Clear it so the composite OnAttach
            // shows the visible grant transition.
            sq.Set(Tags.DivineShield, false);
            s.Log.Add("(For demo, clear intrinsic DS to see OnAttach grant it back.)");

            Entity? blessed = null;
            s.Steps.Add(("Attach Blessed Champion", () =>
            {
                blessed = _engine.SpawnModifier(_catalogs.Modifiers[MOD_BLESSED], sq);
                s.Log.Add($"OnAttach: DS=true, Health += 4 = {sq.Get(Tags.Health)}. " +
                          $"Refresh: Atk=1+4={sq.Get(Tags.Attack)}, MaxHP=1+4={sq.Get(Tags.MaxHealth)}.");
            }));
            s.Steps.Add(("Take 3 damage", () =>
            {
                sq.Set(Tags.Health, sq.Get(Tags.Health) - 3);
                s.Log.Add($"Damage: Health -= 3 → {sq.Get(Tags.Health)}/{sq.Get(Tags.MaxHealth)} (transient).");
            }));
            s.Steps.Add(("Take a hit (pop DS)", () =>
            {
                sq.Set(Tags.DivineShield, false);
                s.Log.Add("Hit → DS popped (transient write, no refresh).");
            }));
            s.Steps.Add(("Detach Blessed", () =>
            {
                if (blessed != null) _engine.DestroyModifier(blessed);
                s.Log.Add($"Refresh: Atk={sq.Get(Tags.Attack)} (base 1), MaxHP={sq.Get(Tags.MaxHealth)} (base 1), Health untouched={sq.Get(Tags.Health)}, DS={sq.Get(Tags.DivineShield)}. ✓");
            }));
            return s;
        }

        private Scenario BuildScenario_ConditionalDependency()
        {
            var s = new Scenario { Title = "Dependent modifiers via Order" };
            var d = _engine.SpawnCard(_catalogs.Cards[CARD_DEFENDER]);
            s.Target = d;
            s.Log.Add($"Base: Atk={d.Get(Tags.Attack)}, Def={d.Get(Tags.Defense)}.");

            Entity? iron = null, cond = null;
            s.Steps.Add(("Attach Conditional Charge alone", () =>
            {
                cond = _engine.SpawnModifier(_catalogs.Modifiers[MOD_CONDCHARGE], d);
                s.Log.Add($"Refresh: Def={d.Get(Tags.Defense)} (≤5) → else +1 → Atk={d.Get(Tags.Attack)}.");
            }));
            s.Steps.Add(("Attach Iron Plating", () =>
            {
                iron = _engine.SpawnModifier(_catalogs.Modifiers[MOD_IRON], d);
                s.Log.Add($"Refresh order: Iron(100) → Def={d.Get(Tags.Defense)}. Cond(200) → >5 → +3 ATK → Atk={d.Get(Tags.Attack)}. ✓");
            }));
            s.Steps.Add(("Detach Iron Plating", () =>
            {
                if (iron != null) _engine.DestroyModifier(iron);
                s.Log.Add($"Refresh: Def={d.Get(Tags.Defense)}. Cond ≤5 → else +1 → Atk={d.Get(Tags.Attack)}. ✓");
            }));
            return s;
        }

        // ─── IMGUI ───────────────────────────────────────────────────────

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(8, 8, Screen.width - 16, Screen.height - 16));

            GUILayout.BeginHorizontal();
            GUILayout.Label("Modifiers Spike — concept-level validation",
                            GUI.skin.box, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Reset", GUILayout.Width(100))) Rebuild();
            GUILayout.EndHorizontal();
            GUILayout.Space(4);

            GUILayout.Label(
                $"Catalogs.Cards: {_catalogs.Cards.Count}    " +
                $"Catalogs.Modifiers: {_catalogs.Modifiers.Count}    " +
                $"EntityStore.All: {_engine.EntityStore.All.Count}");
            GUILayout.Space(6);

            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var s in _scenarios) RenderScenario(s);
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private void RenderScenario(Scenario s)
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(s.Title, GUI.skin.box);

            // Step buttons — left-to-right; current step highlighted.
            GUILayout.BeginHorizontal();
            for (int i = 0; i < s.Steps.Count; i++)
            {
                var (label, action) = s.Steps[i];
                bool isNext = i == s.Step;
                var prev = GUI.color;
                GUI.color = isNext ? Color.cyan : (i < s.Step ? new Color(0.7f, 0.7f, 0.7f) : Color.white);
                if (GUILayout.Button(label) && i == s.Step)
                {
                    action();
                    s.Step++;
                }
                GUI.color = prev;
            }
            GUILayout.EndHorizontal();

            // Target state.
            GUILayout.Label($"Target: {Describe(s.Target)}");
            foreach (var kv in s.Target.Slots.OrderBy(k => k.Key))
                GUILayout.Label($"      slot {kv.Key,3} = {kv.Value}");

            // Modifiers targeting this entity.
            var mods = _engine.OrderedModifiersFor(s.Target).ToList();
            GUILayout.Label($"Modifiers ({mods.Count}):");
            foreach (var m in mods)
            {
                _catalogs.Modifiers.TryGet(m.DefinitionId, out var def);
                GUILayout.Label($"      #{m.Id}  Def#{m.DefinitionId} '{def?.Name}'  Order={def?.Order}  Tooltip='{def?.Tooltip?.Label}'");
            }

            // Step log.
            GUILayout.Label("Log:");
            foreach (var line in s.Log) GUILayout.Label("   " + line);

            GUILayout.EndVertical();
            GUILayout.Space(6);
        }

        private string Describe(Entity e)
        {
            if (e.DefinitionId == 0) return $"Entity#{e.Id} ({e.Type.Name})";
            _catalogs.Cards.TryGet(e.DefinitionId, out var card);
            return $"Entity#{e.Id} ({e.Type.Name}) Def#{e.DefinitionId} '{card?.Name}'";
        }
    }
}
