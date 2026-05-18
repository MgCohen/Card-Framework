#nullable enable
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Spike.Modifiers.Editor
{
    // Headless validation. Drives the four worked scenarios from
    // modifiers-as-entities.md programmatically, asserting the expected
    // tag values at every checkpoint. Run from the menu or in batch via
    //   -executeMethod Spike.Modifiers.Editor.ModifiersHeadlessValidation.Run
    //
    // Logs PASS / FAIL per assertion and a final summary. Non-zero
    // FAIL count makes the batch run surface them as Debug.LogError so
    // they're visible in the log tail.
    public static class ModifiersHeadlessValidation
    {
        private static int _pass;
        private static int _fail;
        private static readonly List<string> _failures = new();

        [MenuItem("Tools/Modifiers/Run Headless Validation")]
        public static void Run()
        {
            _pass = 0; _fail = 0; _failures.Clear();

            Debug.Log("[ModifiersValidation] Begin.");
            Scenario_TransientGrant();
            Scenario_RebaseableOrdering();
            Scenario_BlessedComposite();
            Scenario_ConditionalDependency();

            string summary = $"[ModifiersValidation] Done — PASS={_pass} FAIL={_fail}";
            if (_fail == 0) Debug.Log(summary);
            else
            {
                foreach (var f in _failures) Debug.LogError("[ModifiersValidation] FAIL: " + f);
                Debug.LogError(summary);
            }
        }

        // ─── Scenarios ───────────────────────────────────────────────────

        private static void Scenario_TransientGrant()
        {
            const string S = "transient-grant";
            var engine = BootEngine();
            var raptor = engine.SpawnCard(engine.Catalogs.Cards[ModifiersBootstrap.CARD_RAPTOR]);

            AssertEq($"{S}/base.attack",       3,    raptor.Get(Tags.Attack));
            AssertEq($"{S}/base.health",       2,    raptor.Get(Tags.Health));
            AssertEq($"{S}/base.maxHealth",    2,    raptor.Get(Tags.MaxHealth));
            AssertEq($"{S}/base.divineShield", false, raptor.Get(Tags.DivineShield));

            var hopA = engine.SpawnModifier(engine.Catalogs.Modifiers[ModifiersBootstrap.MOD_HOP], raptor);
            AssertEq($"{S}/after attach HoP_A.divineShield", true, raptor.Get(Tags.DivineShield));

            var hopB = engine.SpawnModifier(engine.Catalogs.Modifiers[ModifiersBootstrap.MOD_HOP], raptor);
            AssertEq($"{S}/after attach HoP_B.divineShield", true, raptor.Get(Tags.DivineShield));

            // Hit consumes DS — gameplay write.
            raptor.Set(Tags.DivineShield, false);
            AssertEq($"{S}/after hit.divineShield", false, raptor.Get(Tags.DivineShield));

            engine.DestroyModifier(hopA);
            AssertEq($"{S}/after detach HoP_A.divineShield", false, raptor.Get(Tags.DivineShield));

            engine.DestroyModifier(hopB);
            AssertEq($"{S}/after detach HoP_B.divineShield", false, raptor.Get(Tags.DivineShield));

            var hopC = engine.SpawnModifier(engine.Catalogs.Modifiers[ModifiersBootstrap.MOD_HOP], raptor);
            AssertEq($"{S}/after attach HoP_C.divineShield", true, raptor.Get(Tags.DivineShield));
        }

        private static void Scenario_RebaseableOrdering()
        {
            const string S = "rebaseable-ordering";
            var engine = BootEngine();
            var sq = engine.SpawnCard(engine.Catalogs.Cards[ModifiersBootstrap.CARD_SQUIRE]);
            AssertEq($"{S}/base.attack", 1, sq.Get(Tags.Attack));

            var add = engine.SpawnModifier(engine.Catalogs.Modifiers[ModifiersBootstrap.MOD_ADD3_ATK], sq);
            AssertEq($"{S}/after +3.attack", 4, sq.Get(Tags.Attack));

            var mul = engine.SpawnModifier(engine.Catalogs.Modifiers[ModifiersBootstrap.MOD_MUL2_ATK], sq);
            AssertEq($"{S}/after x2.attack", 8, sq.Get(Tags.Attack));

            var set = engine.SpawnModifier(engine.Catalogs.Modifiers[ModifiersBootstrap.MOD_SET1_ATK], sq);
            AssertEq($"{S}/after Set1.attack", 1, sq.Get(Tags.Attack));

            engine.DestroyModifier(set);
            AssertEq($"{S}/detach Set1.attack", 8, sq.Get(Tags.Attack));

            engine.DestroyModifier(add);
            // Headline: (1+3)*2 - 3 = 5 (wrong, stateful undo). Refresh-from-base = 1*2 = 2.
            AssertEq($"{S}/detach +3.attack (vs. stateful 5)", 2, sq.Get(Tags.Attack));

            engine.DestroyModifier(mul);
            AssertEq($"{S}/detach x2.attack", 1, sq.Get(Tags.Attack));
        }

        private static void Scenario_BlessedComposite()
        {
            const string S = "blessed-composite";
            var engine = BootEngine();
            var sq = engine.SpawnCard(engine.Catalogs.Cards[ModifiersBootstrap.CARD_SQUIRE]);
            // Match the doc's worked example: vanilla 1/1 base, no intrinsic DS.
            sq.Set(Tags.DivineShield, false);

            AssertEq($"{S}/base.attack",    1, sq.Get(Tags.Attack));
            AssertEq($"{S}/base.maxHealth", 1, sq.Get(Tags.MaxHealth));
            AssertEq($"{S}/base.health",    1, sq.Get(Tags.Health));

            var blessed = engine.SpawnModifier(engine.Catalogs.Modifiers[ModifiersBootstrap.MOD_BLESSED], sq);
            AssertEq($"{S}/after attach.attack",       5,    sq.Get(Tags.Attack));
            AssertEq($"{S}/after attach.maxHealth",    5,    sq.Get(Tags.MaxHealth));
            AssertEq($"{S}/after attach.health",       5,    sq.Get(Tags.Health));
            AssertEq($"{S}/after attach.divineShield", true, sq.Get(Tags.DivineShield));

            // Take 3 damage — gameplay transient write.
            sq.Set(Tags.Health, sq.Get(Tags.Health) - 3);
            AssertEq($"{S}/after damage.health", 2, sq.Get(Tags.Health));

            // Pop DS — gameplay transient write.
            sq.Set(Tags.DivineShield, false);
            AssertEq($"{S}/after pop.divineShield", false, sq.Get(Tags.DivineShield));

            engine.DestroyModifier(blessed);
            AssertEq($"{S}/after detach.attack",       1,     sq.Get(Tags.Attack));
            AssertEq($"{S}/after detach.maxHealth",    1,     sq.Get(Tags.MaxHealth));
            AssertEq($"{S}/after detach.health",       2,     sq.Get(Tags.Health));      // transient — survives
            AssertEq($"{S}/after detach.divineShield", false, sq.Get(Tags.DivineShield)); // transient — survives
        }

        private static void Scenario_ConditionalDependency()
        {
            const string S = "conditional-dependency";
            var engine = BootEngine();
            var d = engine.SpawnCard(engine.Catalogs.Cards[ModifiersBootstrap.CARD_DEFENDER]);

            AssertEq($"{S}/base.attack",  1, d.Get(Tags.Attack));
            AssertEq($"{S}/base.defense", 2, d.Get(Tags.Defense));

            var cond = engine.SpawnModifier(engine.Catalogs.Modifiers[ModifiersBootstrap.MOD_CONDCHARGE], d);
            // Defense=2 (≤5) → else branch → +1 attack.
            AssertEq($"{S}/after cond-only.attack",  2, d.Get(Tags.Attack));
            AssertEq($"{S}/after cond-only.defense", 2, d.Get(Tags.Defense));

            var iron = engine.SpawnModifier(engine.Catalogs.Modifiers[ModifiersBootstrap.MOD_IRON], d);
            // Iron (Order 100) runs first → Defense=7. Then Conditional (Order 200)
            // sees Defense>5 → +3 attack → Attack=1+3=4.
            AssertEq($"{S}/after iron.defense", 7, d.Get(Tags.Defense));
            AssertEq($"{S}/after iron.attack",  4, d.Get(Tags.Attack));

            engine.DestroyModifier(iron);
            // Iron gone → Defense=base(2). Cond sees ≤5 → +1 → Attack=2.
            AssertEq($"{S}/detach iron.defense", 2, d.Get(Tags.Defense));
            AssertEq($"{S}/detach iron.attack",  2, d.Get(Tags.Attack));
        }

        // ─── Plumbing ────────────────────────────────────────────────────

        private static GameEngine BootEngine()
        {
            var catalogs = ModifiersBootstrap.BuildCatalogs();
            return new GameEngine(catalogs);
        }

        private static void AssertEq<T>(string label, T expected, T actual)
        {
            if (EqualityComparer<T>.Default.Equals(expected, actual))
            {
                _pass++;
                Debug.Log($"[ModifiersValidation] PASS  {label}  = {actual}");
            }
            else
            {
                _fail++;
                _failures.Add($"{label}  expected={expected}  actual={actual}");
            }
        }
    }
}
