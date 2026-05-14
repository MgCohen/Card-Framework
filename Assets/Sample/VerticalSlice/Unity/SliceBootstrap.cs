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
    /// effects fired via <c>Effects.Run</c>. No more entity subclasses —
    /// caster/target are plain <see cref="Entity"/> with marker tags.
    /// </summary>
    public class SliceBootstrap : MonoBehaviour
    {
        private GameEngine _engine = null!;
        private MirrorWorld _mirror = null!;
        private ScrubController _scrub = null!;

        private Entity? _p1;
        private Entity? _p2;
        private Entity? _caster;     // P1
        private Entity? _ally1;      // P1 — passive minion to demo Deathrattle AOE
        private Entity? _target;     // P2
        private Entity? _ally2;      // P2 — passive minion
        private Entity? _hand;
        private Entity? _battlefield;
        private Entity? _graveyard;

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

            // Hook effects (global rules) — Effect<DealDamage> shape with
            // Register(...) on each rule. Phase/priority lives at the
            // registration call site, not on the rule class.
            DivineShieldRule.Register(_engine.Effects);
            ArmorRule.Register(_engine.Effects);
            BacklashRule.Register(_engine.Effects);

            // Bridge: translates broadcast MoveCard→graveyard into direct
            // Deathrattle trigger on the dying card. Per-card deathrattle
            // EFFECT is what's specific; the bridge is uniform.
            DeathrattleBridge.Register(_engine.Commands);

            _p1 = _engine.World.Create(); _p1.MarkAsPlayer("P1");
            _p2 = _engine.World.Create(); _p2.MarkAsPlayer("P2");

            _hand        = _engine.World.Create(); _hand.MarkAsZone("Hand");
            _battlefield = _engine.World.Create(); _battlefield.MarkAsZone("Battlefield");
            _graveyard   = _engine.World.Create(); _graveyard.MarkAsGraveyard();

            // P1's cards
            _caster = _engine.World.Create();
            _caster.MarkAsCard();
            _caster.SetCardName("Caster");
            _caster.SetHp(30); _caster.SetMaxHp(30); _caster.SetAttack(2);
            _caster.SetOwner(_p1);
            _caster.SetZone(_battlefield);
            // Caster's deathrattle: damage all enemies.
            _engine.CardEffects.Set<Deathrattle>(_caster,
                t => new DamageAllEnemies(t));

            _ally1 = _engine.World.Create();
            _ally1.MarkAsCard();
            _ally1.SetCardName("Ally-P1");
            _ally1.SetHp(10); _ally1.SetMaxHp(10);
            _ally1.SetOwner(_p1);
            _ally1.SetZone(_battlefield);
            // Ally-P1's deathrattle: heal all allies. Different behavior on a
            // different card via the same machinery — no subclass per card.
            _engine.CardEffects.Set<Deathrattle>(_ally1,
                t => new HealAllAllies(t));

            // P2's cards — no deathrattles attached.
            _target = _engine.World.Create();
            _target.MarkAsCard();
            _target.SetCardName("Target");
            _target.SetHp(30); _target.SetMaxHp(30); _target.SetAttack(0); _target.SetArmor(0);
            _target.SetOwner(_p2);
            _target.SetZone(_battlefield);

            _ally2 = _engine.World.Create();
            _ally2.MarkAsCard();
            _ally2.SetCardName("Ally-P2");
            _ally2.SetHp(10); _ally2.SetMaxHp(10);
            _ally2.SetOwner(_p2);
            _ally2.SetZone(_battlefield);

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

            if (GUILayout.Button("Cast Tactician", GUILayout.Width(130))
                && _caster != null && _target != null)
            {
                _engine.Effects.Run(new Tactician(_caster, _target));
                AfterCommandIssued();
            }

            GUILayout.EndHorizontal();

            // --- Open requests (Choose-One pickers) ------------------------
            if (_engine.Requests.Open.Count > 0)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("OPEN REQUESTS");
                // Snapshot so a Respond inside the loop doesn't mutate-mid-iterate.
                var openSnapshot = _engine.Requests.Open.ToList();
                foreach (var kvp in openSnapshot)
                {
                    var req = kvp.Value;
                    GUILayout.Label($"#{kvp.Key} {req.Name}");
                    GUILayout.BeginHorizontal();
                    foreach (var opt in req.Options)
                    {
                        var label = OptionButtonLabel(opt);
                        var width = opt is CancelOptionEvent ? 90 : 220;
                        if (GUILayout.Button(label, GUILayout.Width(width)))
                        {
                            _engine.Requests.Respond(kvp.Key, opt.Index);
                            AfterCommandIssued();
                        }
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();
            }

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

            // --- Deathrattle info (attached per-card at setup) --------------
            GUILayout.Label("Deathrattles (per-card via CardEffectStore):");
            GUILayout.Label("  Caster  → DamageAllEnemies   (move Caster to Graveyard to fire)");
            GUILayout.Label("  Ally-P1 → HealAllAllies      (move Ally-P1 to Graveyard to fire)");

            // --- Zone movement buttons --------------------------------------
            if (_caster != null && _target != null
                && _hand != null && _battlefield != null && _graveyard != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Move Caster:", GUILayout.Width(100));
                if (GUILayout.Button("→ Hand", GUILayout.Width(80)))
                    { _engine.Commands.Execute(new MoveCard(_caster, _hand)); AfterCommandIssued(); }
                if (GUILayout.Button("→ Battlefield", GUILayout.Width(110)))
                    { _engine.Commands.Execute(new MoveCard(_caster, _battlefield)); AfterCommandIssued(); }
                if (GUILayout.Button("→ Graveyard", GUILayout.Width(100)))
                    { _engine.Commands.Execute(new MoveCard(_caster, _graveyard)); AfterCommandIssued(); }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Move Target:", GUILayout.Width(100));
                if (GUILayout.Button("→ Hand", GUILayout.Width(80)))
                    { _engine.Commands.Execute(new MoveCard(_target, _hand)); AfterCommandIssued(); }
                if (GUILayout.Button("→ Battlefield", GUILayout.Width(110)))
                    { _engine.Commands.Execute(new MoveCard(_target, _battlefield)); AfterCommandIssued(); }
                if (GUILayout.Button("→ Graveyard", GUILayout.Width(100)))
                    { _engine.Commands.Execute(new MoveCard(_target, _graveyard)); AfterCommandIssued(); }
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
                        var flags = new System.Text.StringBuilder();
                        if (c.DivineShield()) flags.Append("  shield");
                        if (c.Backlash())     flags.Append("  backlash");
                        if (_engine.CardEffects.Has<Deathrattle>(c)) flags.Append("  deathrattle");
                        GUILayout.Label($"    #{c.Id} {c.CardName()} (owner={ownerLabel})  hp={c.Hp()}  armor={c.Armor()}{flags}");
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

        // Generic debug-friendly button text for an option wire event.
        // Labels live on the client (here); the wire carries pure data
        // (refs + value). For real games the client would use richer UI
        // and look up display strings from a content table.
        private static string OptionButtonLabel(OptionEvent opt) => opt switch
        {
            CancelOptionEvent => "Cancel",
            PickOptionEvent pick =>
                $"Pick {pick.Index}" +
                (pick.SourceId.HasValue ? $"  src=#{pick.SourceId.Value}" : "") +
                (pick.TargetId.HasValue ? $"  tgt=#{pick.TargetId.Value}" : "") +
                (pick.Value.HasValue   ? $"  val={pick.Value.Value}"     : ""),
            _ => $"Option {opt.Index}",
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
