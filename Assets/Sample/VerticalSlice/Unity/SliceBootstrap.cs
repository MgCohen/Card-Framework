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
    /// Slice bootstrap. Drop on any GameObject in a scene and Play.
    ///
    /// Hearthstone-shaped: Jaina (Mage) vs Garrosh (Warrior) heroes,
    /// each with their own Hand/Battlefield/Graveyard zones. Spells live
    /// as card entities in the hand (IsSpell marker, Cost tag). Casting a
    /// spell button moves the card from Hand → Graveyard and runs the
    /// effect — the same flow real HS uses.
    ///
    /// Stats follow real card values: Chillwind Yeti 4/5, Bloodfen Raptor
    /// 3/2, Abomination 4/4 (Deathrattle), Fireball 6 dmg / Healing Touch
    /// 8 heal. Tomb Tender is invented (1/3, Deathrattle: heal allies).
    /// </summary>
    public class SliceBootstrap : MonoBehaviour
    {
        private GameEngine _engine = null!;
        private MirrorWorld _mirror = null!;
        private ScrubController _scrub = null!;

        // Heroes — players ARE the heroes in this slice.
        private Entity? _jaina;
        private Entity? _garrosh;

        // Jaina's side.
        private Entity? _jainaHand;
        private Entity? _jainaBattlefield;
        private Entity? _jainaGraveyard;
        private Entity? _abomination;     // P1 minion — Deathrattle (AOE damage)
        private Entity? _tombTender;      // P1 minion — Deathrattle (AOE heal)
        private Entity? _spiteImp;        // P1 minion — Backlash
        private Entity? _jainaFireball;
        private Entity? _jainaHealingTouch;

        // Garrosh's side.
        private Entity? _garroshHand;
        private Entity? _garroshBattlefield;
        private Entity? _garroshGraveyard;
        private Entity? _yeti;            // P2 minion — vanilla 4/5
        private Entity? _raptor;          // P2 minion — vanilla 3/2
        private Entity? _argentSquire;    // P2 minion — Divine Shield
        private Entity? _garroshFireball;
        private Entity? _garroshHealingTouch;

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

            DivineShieldRule.Register(_engine.Effects);
            ArmorRule.Register(_engine.Effects);
            BacklashRule.Register(_engine.Effects);
            DeathrattleBridge.Register(_engine.Commands);

            // --- Heroes (player + health stats) -----------------------------
            _jaina = _engine.World.Create();
            _jaina.MarkAsPlayer("Jaina");
            _jaina.SetHealth(30); _jaina.SetMaxHealth(30); _jaina.SetArmor(0);

            _garrosh = _engine.World.Create();
            _garrosh.MarkAsPlayer("Garrosh");
            // Warrior class: 30 HP + 5 starting armor. Demos ArmorRule
            // intrinsically — Fireball at Garrosh hits 5 armor first.
            _garrosh.SetHealth(30); _garrosh.SetMaxHealth(30); _garrosh.SetArmor(5);

            // --- Per-player zones -------------------------------------------
            _jainaHand        = MakeZone("Hand",        _jaina);
            _jainaBattlefield = MakeBattlefield("Battlefield", _jaina);
            _jainaGraveyard   = MakeGraveyard("Graveyard", _jaina);

            _garroshHand        = MakeZone("Hand",        _garrosh);
            _garroshBattlefield = MakeBattlefield("Battlefield", _garrosh);
            _garroshGraveyard   = MakeGraveyard("Graveyard", _garrosh);

            // --- Jaina's minions — Deathrattle x2 + Backlash ----------------
            _abomination = MakeMinion("Abomination", cost: 5, atk: 4, hp: 4, owner: _jaina, zone: _jainaBattlefield);
            _engine.CardEffects.Set<Deathrattle>(_abomination, t => new DamageAllEnemies(t));

            _tombTender = MakeMinion("Tomb Tender", cost: 2, atk: 1, hp: 3, owner: _jaina, zone: _jainaBattlefield);
            _engine.CardEffects.Set<Deathrattle>(_tombTender, t => new HealAllAllies(t));

            _spiteImp = MakeMinion("Spite Imp", cost: 3, atk: 2, hp: 3, owner: _jaina, zone: _jainaBattlefield);
            _spiteImp.SetBacklash(true);   // intrinsic — Backlash bakes in at spawn

            // --- Garrosh's minions — vanilla x2 + Divine Shield -------------
            _yeti   = MakeMinion("Chillwind Yeti",   cost: 4, atk: 4, hp: 5, owner: _garrosh, zone: _garroshBattlefield);
            _raptor = MakeMinion("Bloodfen Raptor",  cost: 2, atk: 3, hp: 2, owner: _garrosh, zone: _garroshBattlefield);

            _argentSquire = MakeMinion("Argent Squire", cost: 1, atk: 1, hp: 1, owner: _garrosh, zone: _garroshBattlefield);
            _argentSquire.SetDivineShield(true);   // intrinsic Divine Shield

            // --- Spells in hand (one set per player) ------------------------
            _jainaFireball     = MakeSpell("Fireball",      cost: 4, owner: _jaina,   zone: _jainaHand);
            _jainaHealingTouch = MakeSpell("Healing Touch", cost: 3, owner: _jaina,   zone: _jainaHand);
            _garroshFireball     = MakeSpell("Fireball",      cost: 4, owner: _garrosh, zone: _garroshHand);
            _garroshHealingTouch = MakeSpell("Healing Touch", cost: 3, owner: _garrosh, zone: _garroshHand);

            _mirror = new MirrorWorld();
            _scrub = new ScrubController(_mirror, _engine.World.Log.Events);

            _sliderTracksHead = true;
            _sliderPos = _engine.World.Log.Count;
            _scrub.GoTo(_sliderPos);
        }

        // --- Setup helpers ---------------------------------------------------

        private Entity MakeZone(string name, Entity owner)
        {
            var z = _engine.World.Create();
            z.MarkAsZone(name);
            z.SetOwner(owner);
            return z;
        }

        private Entity MakeBattlefield(string name, Entity owner)
        {
            var z = _engine.World.Create();
            z.MarkAsBattlefield(name);
            z.SetOwner(owner);
            return z;
        }

        private Entity MakeGraveyard(string name, Entity owner)
        {
            var z = _engine.World.Create();
            z.MarkAsGraveyard(name);
            z.SetOwner(owner);
            return z;
        }

        private Entity MakeMinion(string name, int cost, int atk, int hp, Entity owner, Entity zone)
        {
            var m = _engine.World.Create();
            m.MarkAsMinion(name);
            m.SetCost(cost); m.SetAttack(atk); m.SetHealth(hp); m.SetMaxHealth(hp);
            m.SetOwner(owner); m.SetZone(zone);
            return m;
        }

        private Entity MakeSpell(string name, int cost, Entity owner, Entity zone)
        {
            var s = _engine.World.Create();
            s.MarkAsSpell(name);
            s.SetCost(cost);
            s.SetOwner(owner); s.SetZone(zone);
            return s;
        }

        // Move the spell card to its owner's graveyard, then run the effect.
        // Matches HS spell-cast flow: pay (skipped), card → graveyard, effect resolves.
        private void CastFireball(Entity caster, Entity spellCard, IEntity? target)
        {
            if (target == null) return;
            var graveyard = caster == _jaina ? _jainaGraveyard! : _garroshGraveyard!;
            _engine.Commands.Execute(new MoveCard(spellCard, graveyard));
            _engine.Effects.Run(new Fireball(caster, target));
            AfterCommandIssued();
        }

        private void CastHealingTouch(Entity caster, Entity spellCard, IEntity? target)
        {
            if (target == null) return;
            var graveyard = caster == _jaina ? _jainaGraveyard! : _garroshGraveyard!;
            _engine.Commands.Execute(new MoveCard(spellCard, graveyard));
            _engine.Effects.Run(new HealingTouch(caster, target));
            AfterCommandIssued();
        }

        private void AfterCommandIssued()
        {
            _sliderTracksHead = true;
            _sliderPos = _engine.World.Log.Count;
            _scrub.GoTo(_sliderPos);
        }

        // --- GUI -------------------------------------------------------------

        private void OnGUI()
        {
            GUILayout.BeginVertical();

            // --- Controls ----------------------------------------------------
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset", GUILayout.Width(80))) Setup();
            GUILayout.EndHorizontal();

            // Jaina's actions.
            if (_jaina != null && _jainaFireball != null && _jainaHealingTouch != null
                && _garrosh != null && _argentSquire != null && _spiteImp != null && _yeti != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Jaina casts:", GUILayout.Width(90));
                if (_jainaFireball.ZoneId() == _jainaHand?.Id)
                {
                    // Garrosh has 5 armor → Fireball does 1 net dmg (demos ArmorRule).
                    if (GUILayout.Button("Fireball → Garrosh", GUILayout.Width(160)))
                        CastFireball(_jaina, _jainaFireball, _garrosh);
                    // Argent Squire has DS → fully absorbed (demos DivineShieldRule).
                    if (GUILayout.Button("Fireball → Argent Squire", GUILayout.Width(190)))
                        CastFireball(_jaina, _jainaFireball, _argentSquire);
                }
                if (_jainaHealingTouch.ZoneId() == _jainaHand?.Id)
                {
                    if (GUILayout.Button("Healing Touch → Spite Imp", GUILayout.Width(200)))
                        CastHealingTouch(_jaina, _jainaHealingTouch, _spiteImp);
                }
                // Tactician sourced from Spite Imp → picking damage triggers
                // Backlash (Yeti deals 1 back to Spite Imp). Demos BacklashRule
                // without needing an attack action.
                if (GUILayout.Button("Tactician → Yeti  (Spite Imp)", GUILayout.Width(220)))
                {
                    _engine.Effects.Run(new Tactician(_spiteImp, _yeti));
                    AfterCommandIssued();
                }
                GUILayout.EndHorizontal();
            }

            // Garrosh's actions.
            if (_garrosh != null && _garroshFireball != null && _garroshHealingTouch != null
                && _jaina != null && _abomination != null && _yeti != null && _spiteImp != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Garrosh casts:", GUILayout.Width(110));
                if (_garroshFireball.ZoneId() == _garroshHand?.Id)
                {
                    if (GUILayout.Button("Fireball → Jaina", GUILayout.Width(140)))
                        CastFireball(_garrosh, _garroshFireball, _jaina);
                    if (GUILayout.Button("Fireball → Abomination", GUILayout.Width(180)))
                        CastFireball(_garrosh, _garroshFireball, _abomination);
                    if (GUILayout.Button("Fireball → Spite Imp", GUILayout.Width(170)))
                        CastFireball(_garrosh, _garroshFireball, _spiteImp);
                }
                if (_garroshHealingTouch.ZoneId() == _garroshHand?.Id)
                {
                    if (GUILayout.Button("Healing Touch → Yeti", GUILayout.Width(170)))
                        CastHealingTouch(_garrosh, _garroshHealingTouch, _yeti);
                }
                GUILayout.EndHorizontal();
            }

            // --- Open requests (Choose-One pickers) ------------------------
            if (_engine.Requests.Open.Count > 0)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label("OPEN REQUESTS");
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

            // --- Move-to-graveyard (deathrattle demo) -----------------------
            // Only deathrattle-bearing minions have a graveyard button — the
            // demo value is the trigger fire, not the move itself.
            GUILayout.Label("Send a deathrattle-bearing minion to the graveyard to fire its on-death effect:");
            GUILayout.BeginHorizontal();
            if (_abomination != null && _jainaGraveyard != null
                && GUILayout.Button("Abomination → Graveyard  (DR: damage all enemies)", GUILayout.Width(360)))
                { _engine.Commands.Execute(new MoveCard(_abomination, _jainaGraveyard)); AfterCommandIssued(); }
            if (_tombTender != null && _jainaGraveyard != null
                && GUILayout.Button("Tomb Tender → Graveyard  (DR: heal all allies)", GUILayout.Width(330)))
                { _engine.Commands.Execute(new MoveCard(_tombTender, _jainaGraveyard)); AfterCommandIssued(); }
            GUILayout.EndHorizontal();

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
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(360));
            GUILayout.Label("PRIMARY STATE (engine)");
            _primaryScroll = GUILayout.BeginScrollView(_primaryScroll, GUILayout.Height(320));
            foreach (var kvp in _engine.World.Entities)
            {
                var ent = kvp.Value;
                var kind = ent.IsMinion() ? "minion"
                         : ent.IsSpell() ? "spell"
                         : ent.IsCard() ? "card"
                         : ent.IsZone() ? (ent.IsGraveyard() ? "graveyard"
                                          : ent.IsBattlefield() ? "battlefield" : "zone")
                         : ent.IsPlayer() ? "hero"
                         : "";
                var label = !string.IsNullOrEmpty(ent.CardName()) ? ent.CardName()
                          : !string.IsNullOrEmpty(ent.PlayerName()) ? ent.PlayerName()
                          : !string.IsNullOrEmpty(ent.ZoneName()) ? ent.ZoneName()
                          : "";
                GUILayout.Label($"#{ent.Id} [{kind}] {label}".TrimEnd());
                if (ent.Has(Tags.Owner))          GUILayout.Label($"  owner        = #{ent.OwnerId()}");
                if (ent.Has(Tags.Cost))           GUILayout.Label($"  cost         = {ent.Cost()}");
                if (ent.Has(Tags.Health))         GUILayout.Label($"  health       = {ent.Health()}");
                if (ent.Has(Tags.MaxHealth))      GUILayout.Label($"  maxHealth    = {ent.MaxHealth()}");
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
                    GUILayout.Label($"  {t.Key,-14} = {Stringify(t.Value)}");
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

            // --- Per-player zones panel -------------------------------------
            GUILayout.Space(8);
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("BOARD (per-player zones — Hand / Battlefield / Graveyard)");
            _zonesScroll = GUILayout.BeginScrollView(_zonesScroll, GUILayout.Height(260));
            RenderPlayerSection(_jaina);
            GUILayout.Space(6);
            RenderPlayerSection(_garrosh);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.EndVertical();
        }

        private void RenderPlayerSection(Entity? player)
        {
            if (player == null) return;
            GUILayout.Label($"━━ {player.PlayerName()}  (HP {player.Health()}/{player.MaxHealth()}, Armor {player.Armor()})  #{player.Id}");
            foreach (var zone in _engine.World.Zones().Where(z => z.OwnerId() == player.Id)
                                                      .OrderBy(z => z.IsBattlefield() ? 0
                                                                  : z.IsGraveyard()   ? 2 : 1))
            {
                var zoneKind = zone.IsBattlefield() ? "battlefield"
                             : zone.IsGraveyard()   ? "graveyard"
                             : "zone";
                GUILayout.Label($"  {zone.ZoneName()} [{zoneKind}]  #{zone.Id}");
                var cardsInZone = _engine.World.CardsIn(zone).ToList();
                if (cardsInZone.Count == 0)
                {
                    GUILayout.Label("      (empty)");
                    continue;
                }
                foreach (var c in cardsInZone)
                {
                    var flags = new System.Text.StringBuilder();
                    if (c.DivineShield()) flags.Append("  shield");
                    if (c.Backlash())     flags.Append("  backlash");
                    if (_engine.CardEffects.Has<Deathrattle>(c)) flags.Append("  deathrattle");

                    string body = c.IsMinion()
                        ? $"[{c.Cost()}] {c.CardName(),-18} (Minion)  {c.Attack()}/{c.Health()}{flags}"
                        : c.IsSpell()
                            ? $"[{c.Cost()}] {c.CardName(),-18} (Spell)"
                            : $"[{c.Cost()}] {c.CardName(),-18}";
                    GUILayout.Label($"      #{c.Id} {body}");
                }
            }
        }

        private static string Stringify(object? v) => v switch
        {
            null => "null",
            bool b => b.ToString(),
            _ => v.ToString() ?? "null",
        };

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

        // --- Event log rendering -------------------------------------------

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
                return true;
            }
            return false;
        }
    }
}
