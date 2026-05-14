#nullable enable
using System;
using System.Linq;
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
    /// effects fired via <c>Effects.Run</c>. No entity subclasses — every
    /// game thing is an <see cref="Entity"/> with marker tags. Players carry
    /// Hp/MaxHp/Armor directly (the hero IS the player entity); minions are
    /// cards on a per-player Battlefield; spells are cards in a per-player
    /// Hand.
    /// </summary>
    public class SliceBootstrap : MonoBehaviour
    {
        private GameEngine _engine = null!;
        private MirrorWorld _mirror = null!;
        private ScrubController _scrub = null!;

        // Heroes (carry HP/MaxHp/Armor directly).
        private Entity? _p1;
        private Entity? _p2;

        // Per-player zones.
        private Entity? _p1Hand;
        private Entity? _p1Battlefield;
        private Entity? _p1Graveyard;
        private Entity? _p2Hand;
        private Entity? _p2Battlefield;
        private Entity? _p2Graveyard;

        // P1's minions.
        private Entity? _lootHoarder;     // Deathrattle toggle target.
        private Entity? _chillwindYeti;

        // P2's minions.
        private Entity? _argentSquire;    // DivineShield toggle target.
        private Entity? _boulderfistOgre;

        // Spells in hand (one set per player, mirrored).
        private Entity? _p1Fireball;
        private Entity? _p1HealingTouch;
        private Entity? _p2Fireball;
        private Entity? _p2HealingTouch;

        private Vector2 _eventLogScroll;
        private Vector2 _primaryScroll;
        private Vector2 _mirrorScroll;
        private Vector2 _zonesScroll;

        private int _sliderPos;
        private bool _sliderTracksHead = true;

        private void Awake() => Setup();

        private void Setup()
        {
            _engine = new GameEngine();
            DivineShieldRule.Register(_engine.Commands);
            ArmorRule.Register(_engine.Commands);
            BacklashRule.Register(_engine.Commands);
            DeathrattleRule.Register(_engine.Commands);

            // Setup is a flat sequence of EntityCreated + TagSet events; no
            // enclosing scope. Order: players → zones → minions → spells.

            // Heroes — Hp/MaxHp/Armor live directly on the Player entity.
            _p1 = _engine.World.Create();
            _p1.MarkAsPlayer("Player 1");
            _p1.SetHp(30); _p1.SetMaxHp(30); _p1.SetArmor(0);

            _p2 = _engine.World.Create();
            _p2.MarkAsPlayer("Player 2");
            _p2.SetHp(30); _p2.SetMaxHp(30); _p2.SetArmor(0);

            // Per-player zones — one Hand / Battlefield / Graveyard each.
            _p1Hand        = _engine.World.Create(); _p1Hand.MarkAsZone("P1 Hand");                _p1Hand.SetOwner(_p1);
            _p1Battlefield = _engine.World.Create(); _p1Battlefield.MarkAsZone("P1 Battlefield");  _p1Battlefield.SetOwner(_p1);
            _p1Graveyard   = _engine.World.Create(); _p1Graveyard.MarkAsGraveyard("P1 Graveyard"); _p1Graveyard.SetOwner(_p1);

            _p2Hand        = _engine.World.Create(); _p2Hand.MarkAsZone("P2 Hand");                _p2Hand.SetOwner(_p2);
            _p2Battlefield = _engine.World.Create(); _p2Battlefield.MarkAsZone("P2 Battlefield");  _p2Battlefield.SetOwner(_p2);
            _p2Graveyard   = _engine.World.Create(); _p2Graveyard.MarkAsGraveyard("P2 Graveyard"); _p2Graveyard.SetOwner(_p2);

            // P1's minions.
            _lootHoarder = _engine.World.Create();
            _lootHoarder.MarkAsCard();
            _lootHoarder.SetCardName("Loot Hoarder");
            _lootHoarder.SetHp(1); _lootHoarder.SetMaxHp(1); _lootHoarder.SetAttack(2);
            _lootHoarder.SetOwner(_p1);
            _lootHoarder.SetZone(_p1Battlefield);

            _chillwindYeti = _engine.World.Create();
            _chillwindYeti.MarkAsCard();
            _chillwindYeti.SetCardName("Chillwind Yeti");
            _chillwindYeti.SetHp(5); _chillwindYeti.SetMaxHp(5); _chillwindYeti.SetAttack(4);
            _chillwindYeti.SetOwner(_p1);
            _chillwindYeti.SetZone(_p1Battlefield);

            // P2's minions.
            _argentSquire = _engine.World.Create();
            _argentSquire.MarkAsCard();
            _argentSquire.SetCardName("Argent Squire");
            _argentSquire.SetHp(1); _argentSquire.SetMaxHp(1); _argentSquire.SetAttack(1);
            _argentSquire.SetOwner(_p2);
            _argentSquire.SetZone(_p2Battlefield);

            _boulderfistOgre = _engine.World.Create();
            _boulderfistOgre.MarkAsCard();
            _boulderfistOgre.SetCardName("Boulderfist Ogre");
            _boulderfistOgre.SetHp(7); _boulderfistOgre.SetMaxHp(7); _boulderfistOgre.SetAttack(6);
            _boulderfistOgre.SetOwner(_p2);
            _boulderfistOgre.SetZone(_p2Battlefield);

            // Spells in hand — name + owner + zone only. No stats; the spell's
            // behavior lives in the Fireball / HealingPotion Effect classes.
            _p1Fireball = _engine.World.Create();
            _p1Fireball.MarkAsCard(); _p1Fireball.SetCardName("Fireball");
            _p1Fireball.SetOwner(_p1); _p1Fireball.SetZone(_p1Hand);

            _p1HealingTouch = _engine.World.Create();
            _p1HealingTouch.MarkAsCard(); _p1HealingTouch.SetCardName("Healing Touch");
            _p1HealingTouch.SetOwner(_p1); _p1HealingTouch.SetZone(_p1Hand);

            _p2Fireball = _engine.World.Create();
            _p2Fireball.MarkAsCard(); _p2Fireball.SetCardName("Fireball");
            _p2Fireball.SetOwner(_p2); _p2Fireball.SetZone(_p2Hand);

            _p2HealingTouch = _engine.World.Create();
            _p2HealingTouch.MarkAsCard(); _p2HealingTouch.SetCardName("Healing Touch");
            _p2HealingTouch.SetOwner(_p2); _p2HealingTouch.SetZone(_p2Hand);

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
            GUILayout.EndHorizontal();

            // --- Cast spells (P1 acts; P2 is the target side) ---------------
            GUILayout.Space(4);
            GUILayout.Label("P1 casts:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Fireball → P2 Hero", GUILayout.Width(170))
                && _p1 != null && _p2 != null)
            {
                _engine.Effects.Run(new Fireball(_p1, _p2));
                AfterCommandIssued();
            }
            if (GUILayout.Button("Fireball → Argent Squire", GUILayout.Width(200))
                && _p1 != null && _argentSquire != null)
            {
                _engine.Effects.Run(new Fireball(_p1, _argentSquire));
                AfterCommandIssued();
            }
            if (GUILayout.Button("Fireball → Boulderfist", GUILayout.Width(180))
                && _p1 != null && _boulderfistOgre != null)
            {
                _engine.Effects.Run(new Fireball(_p1, _boulderfistOgre));
                AfterCommandIssued();
            }
            if (GUILayout.Button("Healing Touch → P1 Hero", GUILayout.Width(190))
                && _p1 != null)
            {
                _engine.Effects.Run(new HealingPotion(_p1, _p1));
                AfterCommandIssued();
            }
            GUILayout.EndHorizontal();

            // --- Direct tag mutation demo (AC-2 equivalent) -----------------
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Set P2 Hero HP = 25", GUILayout.Width(170)) && _p2 != null)
            {
                _p2.SetHp(25);
                AfterCommandIssued();
            }
            GUILayout.EndHorizontal();

            // --- Hero toggles (Backlash on P1, Armor on P2) -----------------
            GUILayout.Space(4);
            GUILayout.Label("Hero toggles:");
            if (_p1 != null)
            {
                GUILayout.BeginHorizontal();
                var bl = _p1.Backlash();
                var newBl = GUILayout.Toggle(bl, "Backlash on P1 Hero (reflects 1 dmg when P1 deals damage)");
                if (newBl != bl)
                {
                    _p1.SetBacklash(newBl);
                    AfterCommandIssued();
                }
                GUILayout.EndHorizontal();
            }
            if (_p2 != null)
            {
                GUILayout.BeginHorizontal();
                var armor = _p2.Armor();
                GUILayout.Label($"P2 Hero Armor: {armor}", GUILayout.Width(140));
                if (GUILayout.Button("-", GUILayout.Width(30)) && armor > 0)
                {
                    _p2.SetArmor(armor - 1);
                    AfterCommandIssued();
                }
                if (GUILayout.Button("+", GUILayout.Width(30)))
                {
                    _p2.SetArmor(armor + 1);
                    AfterCommandIssued();
                }
                GUILayout.EndHorizontal();
            }

            // --- Minion toggles (DivineShield + Deathrattle) ----------------
            GUILayout.Space(4);
            GUILayout.Label("Minion toggles:");
            if (_argentSquire != null)
            {
                GUILayout.BeginHorizontal();
                var ds = _argentSquire.DivineShield();
                var newDs = GUILayout.Toggle(ds, "Divine Shield on Argent Squire (P2 minion)");
                if (newDs != ds)
                {
                    _argentSquire.SetDivineShield(newDs);
                    AfterCommandIssued();
                }
                GUILayout.EndHorizontal();
            }
            if (_lootHoarder != null)
            {
                GUILayout.BeginHorizontal();
                var dr = _lootHoarder.HasDeathrattle();
                var newDr = GUILayout.Toggle(dr, "Deathrattle on Loot Hoarder (P1 minion — 2 dmg AOE to enemy minions)");
                if (newDr != dr)
                {
                    _lootHoarder.SetHasDeathrattle(newDr);
                    AfterCommandIssued();
                }
                GUILayout.EndHorizontal();
            }

            // --- Move minions to graveyards (fires Deathrattle) -------------
            GUILayout.Space(4);
            GUILayout.Label("Move minion → graveyard:");
            if (_lootHoarder != null && _p1Graveyard != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Loot Hoarder:", GUILayout.Width(130));
                if (GUILayout.Button("→ P1 Graveyard", GUILayout.Width(140)))
                    { _engine.Commands.Execute(new MoveCard(_lootHoarder, _p1Graveyard)); AfterCommandIssued(); }
                GUILayout.EndHorizontal();
            }
            if (_chillwindYeti != null && _p1Graveyard != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Chillwind Yeti:", GUILayout.Width(130));
                if (GUILayout.Button("→ P1 Graveyard", GUILayout.Width(140)))
                    { _engine.Commands.Execute(new MoveCard(_chillwindYeti, _p1Graveyard)); AfterCommandIssued(); }
                GUILayout.EndHorizontal();
            }
            if (_argentSquire != null && _p2Graveyard != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Argent Squire:", GUILayout.Width(130));
                if (GUILayout.Button("→ P2 Graveyard", GUILayout.Width(140)))
                    { _engine.Commands.Execute(new MoveCard(_argentSquire, _p2Graveyard)); AfterCommandIssued(); }
                GUILayout.EndHorizontal();
            }
            if (_boulderfistOgre != null && _p2Graveyard != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Boulderfist Ogre:", GUILayout.Width(130));
                if (GUILayout.Button("→ P2 Graveyard", GUILayout.Width(140)))
                    { _engine.Commands.Execute(new MoveCard(_boulderfistOgre, _p2Graveyard)); AfterCommandIssued(); }
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
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(320));
            GUILayout.Label("PRIMARY STATE (engine)");
            _primaryScroll = GUILayout.BeginScrollView(_primaryScroll, GUILayout.Height(320));
            foreach (var kvp in _engine.World.Entities)
            {
                var ent = kvp.Value;
                var kind = ent.IsCard() ? "card"
                         : ent.IsZone() ? (ent.IsGraveyard() ? "graveyard" : "zone")
                         : ent.IsPlayer() ? "player"
                         : "";
                var label = !string.IsNullOrEmpty(ent.CardName()) ? ent.CardName()
                          : !string.IsNullOrEmpty(ent.PlayerName()) ? ent.PlayerName()
                          : !string.IsNullOrEmpty(ent.ZoneName()) ? ent.ZoneName()
                          : "";
                GUILayout.Label($"#{ent.Id} [{kind}] {label}".TrimEnd());
                if (ent.Has(Tags.Owner))          GUILayout.Label($"  owner        = #{ent.OwnerId()}");
                if (ent.Has(Tags.Hp))             GUILayout.Label($"  hp           = {ent.Hp()}");
                if (ent.Has(Tags.MaxHp))          GUILayout.Label($"  maxHp        = {ent.MaxHp()}");
                if (ent.Has(Tags.Attack))         GUILayout.Label($"  attack       = {ent.Attack()}");
                if (ent.Has(Tags.Armor))          GUILayout.Label($"  armor        = {ent.Armor()}");
                if (ent.Has(Tags.DivineShield))   GUILayout.Label($"  divineShield = {ent.DivineShield()}");
                if (ent.Has(Tags.Backlash))       GUILayout.Label($"  backlash     = {ent.Backlash()}");
                if (ent.Has(Tags.HasDeathrattle)) GUILayout.Label($"  deathrattle  = {ent.HasDeathrattle()}");
                if (ent.Has(Tags.Zone))           GUILayout.Label($"  zone         = #{ent.ZoneId()}");
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

            // --- Zones panel (full width below the main row) -------------
            GUILayout.Space(8);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("ZONES (cards grouped by Zone tag, owners shown by Owner tag — derived from flat ECS storage via query)");
            _zonesScroll = GUILayout.BeginScrollView(_zonesScroll, GUILayout.Height(180));
            foreach (var zone in _engine.World.Zones())
            {
                var zoneKind = zone.IsGraveyard() ? "graveyard" : "zone";
                GUILayout.Label($"#{zone.Id} [{zoneKind}] {zone.ZoneName()}");
                var cardsInZone = _engine.World.CardsIn(zone).ToList();
                if (cardsInZone.Count == 0)
                {
                    GUILayout.Label("    (empty)");
                }
                else
                {
                    foreach (var c in cardsInZone)
                    {
                        var owner = _engine.World.Get(c.OwnerId());
                        var ownerLabel = owner?.PlayerName() ?? "?";
                        var stats = new System.Text.StringBuilder();
                        if (c.Has(Tags.Attack)) stats.Append($"  atk={c.Attack()}");
                        if (c.Has(Tags.Hp))     stats.Append($"  hp={c.Hp()}");
                        if (c.Has(Tags.Armor) && c.Armor() != 0) stats.Append($"  armor={c.Armor()}");
                        if (c.DivineShield())   stats.Append("  shield");
                        if (c.Backlash())       stats.Append("  backlash");
                        if (c.HasDeathrattle()) stats.Append("  deathrattle");
                        GUILayout.Label($"    #{c.Id} {c.CardName()} (owner={ownerLabel}){stats}");
                    }
                }
                GUILayout.Space(2);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

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
