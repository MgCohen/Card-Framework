#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Spike.EntitySetup
{
    /// <summary>
    /// Drop on any GameObject in an empty scene and Play. Builds the
    /// catalog once, the engine on Reset. Spawns two players, each with
    /// hand / battlefield / graveyard, and a handful of cards into them.
    ///
    /// <para>The IMGUI surface lists every spawned entity grouped by
    /// <see cref="EntityType"/>, with each entity's <c>Id</c>,
    /// <c>Type</c>, <c>DefinitionId</c> (resolved to the def's name via
    /// the catalog), and a dump of its tag bag. Validating the
    /// construction flow at a glance.</para>
    ///
    /// <para><b>Reset</b> rebuilds the engine against the same catalogs
    /// instance — content survives, match state restarts. Demonstrates
    /// the lifetime split from Q7.</para>
    /// </summary>
    public sealed class EntitySetupBootstrap : MonoBehaviour
    {
        private Catalogs   _catalogs = null!;
        private GameEngine _engine   = null!;
        private Vector2    _scroll;

        private void Awake()
        {
            _catalogs = BuildCatalogs();     // content — once
            Rebuild();                       // match state — on demand
        }

        // Renamed from Reset() — Unity reserves `Reset` as a magic editor
        // callback that fires on AddComponent, BEFORE Awake. Letting our
        // method bind to it crashes the SceneBuilder's AddComponent call
        // (catalogs are still null at that point).
        private void Rebuild()
        {
            _engine = new GameEngine(_catalogs);
            SetupMatch();
        }

        // ─── Catalog (content) ──────────────────────────────────────────
        // Pure code-defined. ScriptableObject wrapping is a layer that
        // would sit on top of `c.Cards.Add(asset.ToRuntime())` — out of
        // scope for the construction-flow spike.

        private static Catalogs BuildCatalogs()
        {
            var c = new Catalogs();

            c.Cards.Add(new CardDefinition(
                Id: 101, Name: "Fireball", Kind: CardKind.Spell,
                Cost: 4, Attack: 0, Health: 0, DivineShield: false,
                Effects: new[] { new CardEffect("Deal 6 damage to target.") }));

            c.Cards.Add(new CardDefinition(
                Id: 102, Name: "Healing Touch", Kind: CardKind.Spell,
                Cost: 3, Attack: 0, Health: 0, DivineShield: false,
                Effects: new[] { new CardEffect("Restore 8 health.") }));

            c.Cards.Add(new CardDefinition(
                Id: 201, Name: "Argent Squire", Kind: CardKind.Minion,
                Cost: 1, Attack: 1, Health: 1, DivineShield: true,
                Effects: Array.Empty<CardEffect>()));

            c.Cards.Add(new CardDefinition(
                Id: 202, Name: "Chillwind Yeti", Kind: CardKind.Minion,
                Cost: 4, Attack: 4, Health: 5, DivineShield: false,
                Effects: Array.Empty<CardEffect>()));

            c.Cards.Add(new CardDefinition(
                Id: 203, Name: "Bloodfen Raptor", Kind: CardKind.Minion,
                Cost: 2, Attack: 3, Health: 2, DivineShield: false,
                Effects: Array.Empty<CardEffect>()));

            return c;
        }

        // ─── Match setup ───────────────────────────────────────────────
        // Every entity in the match flows through a Spawn* extension —
        // defless ones (players, zones) call L1 directly inside the
        // helper; def-driven ones (cards) go L2 → L3.

        private void SetupMatch()
        {
            // Players + zones — defless, conceptually complete shapes.
            var jaina        = _engine.SpawnPlayer(health: 30, armor: 0);
            var jainaHand    = _engine.SpawnHand(owner: jaina);
            var jainaField   = _engine.SpawnBattlefield(owner: jaina);
            _                = _engine.SpawnGraveyard(owner: jaina);

            var garrosh      = _engine.SpawnPlayer(health: 30, armor: 5);
            var garroshHand  = _engine.SpawnHand(owner: garrosh);
            var garroshField = _engine.SpawnBattlefield(owner: garrosh);
            _                = _engine.SpawnGraveyard(owner: garrosh);

            // Cards — def-driven via the catalog.
            _engine.SpawnCard(_catalogs.Cards[101], owner: jaina,   zone: jainaHand);     // Fireball
            _engine.SpawnCard(_catalogs.Cards[102], owner: jaina,   zone: jainaHand);     // Healing Touch
            _engine.SpawnCard(_catalogs.Cards[201], owner: jaina,   zone: jainaField);    // Argent Squire
            _engine.SpawnCard(_catalogs.Cards[202], owner: garrosh, zone: garroshField);  // Chillwind Yeti
            _engine.SpawnCard(_catalogs.Cards[203], owner: garrosh, zone: garroshField);  // Bloodfen Raptor
            _engine.SpawnCard(_catalogs.Cards[101], owner: garrosh, zone: garroshHand);   // Garrosh's Fireball — same def, different owner
        }

        // ─── IMGUI surface ─────────────────────────────────────────────

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(8, 8, Screen.width - 16, Screen.height - 16));

            GUILayout.BeginHorizontal();
            GUILayout.Label("Entity Setup Spike — construction flow validation",
                            GUI.skin.box, GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Reset", GUILayout.Width(100))) Rebuild();
            GUILayout.EndHorizontal();
            GUILayout.Space(6);

            GUILayout.Label(
                $"Catalogs.Cards: {_catalogs.Cards.Count}     " +
                $"EntityStore.All: {_engine.EntityStore.All.Count}     " +
                $"(content lives across reset; match state rebuilds)");
            GUILayout.Space(6);

            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var (header, group) in GroupedEntities())
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(header);
                foreach (var e in group) RenderEntity(e);
                GUILayout.EndVertical();
                GUILayout.Space(4);
            }
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        private IEnumerable<(string header, IEnumerable<Entity> group)> GroupedEntities()
        {
            var all = _engine.EntityStore.All.Values;
            yield return ("PLAYERS", all.Where(e => e.Type.Equals(EntityTypes.Player)));
            yield return ("ZONES",   all.Where(e => e.Type.Equals(EntityTypes.Zone)));
            yield return ("CARDS",   all.Where(e => e.Type.Equals(EntityTypes.Card)));
        }

        private void RenderEntity(Entity e)
        {
            // Three-field identity line — what makes an entity an entity.
            string defLabel = e.DefinitionId == 0
                ? "—"
                : _catalogs.Cards.TryGet(e.DefinitionId, out var card)
                    ? $"#{e.DefinitionId} '{card.Name}'"
                    : $"#{e.DefinitionId} (unresolved)";
            GUILayout.Label($"  Entity#{e.Id,-3}  Type={e.Type.Name,-7}  Def={defLabel}");

            // Tag bag dump.
            foreach (var kv in e.Slots)
                GUILayout.Label($"      slot {kv.Key,3} = {kv.Value}");
        }
    }
}
