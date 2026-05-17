#nullable enable
using UnityEngine;

namespace Spike.CardPresentation
{
    /// <summary>
    /// Reactive-flow driver for the card-table demo. Builds a
    /// <see cref="World"/> + <see cref="GameSystem"/>, seeds a starting
    /// deck and graveyard, then wires each scene zone to the world via
    /// <see cref="CardZone.SetWorld"/>. After that nothing in this class
    /// runs — zone changes flow:
    /// <code>
    /// drop → VM.RequestMove → System.MoveCard → World.SetCardZone
    ///      → CardChanged → all zones rebind → views update
    /// </code>
    ///
    /// <para><b>This is the seam.</b> Replace <see cref="SeedWorld"/>
    /// (and eventually <see cref="GameSystem"/>) with an engine adapter
    /// when wiring up to a real game.</para>
    /// </summary>
    public sealed class PresentationSandbox : MonoBehaviour
    {
        // Stable zone identifiers — wired into each zone in Start.
        private const int ZoneIdDeck      = 1;
        private const int ZoneIdHand      = 2;
        private const int ZoneIdPlayfield = 3;
        private const int ZoneIdGraveyard = 4;

        [Header("Zones — drag prefab instances from the scene")]
        [SerializeField] private DeckZone? _deck;
        [SerializeField] private HandZone? _hand;
        [SerializeField] private PlayfieldZone? _playfield;
        [SerializeField] private GraveyardZone? _graveyard;

        private World _world = null!;
        private GameSystem _system = null!;

        // Start (not Awake) so every zone's Awake has run first — zones
        // need their layout-group toggling and label binding in place
        // before we kick the reactive flow.
        private void Start()
        {
            _world  = new World();
            _system = new GameSystem(_world);
            _world.System = _system;   // VMs created via GetVM pick this up

            SeedWorld();

            // World.AddCard doesn't fire CardChanged (it's an additive
            // operation, not a mutation), so the system hasn't computed
            // its initial valid-actions list yet. Kick it once here so
            // zones see a populated allow-list during their first Rebind.
            _system.Recompute();

            _deck?.SetWorld     (_world, _system, ZoneIdDeck);
            _hand?.SetWorld     (_world, _system, ZoneIdHand);
            _playfield?.SetWorld(_world, _system, ZoneIdPlayfield);
            _graveyard?.SetWorld(_world, _system, ZoneIdGraveyard);
        }

        // -----------------------------------------------------------------
        // STUB FIXTURE — entities written directly into the world.
        // Replace with engine-driven seeding when wiring up to real state.
        // -----------------------------------------------------------------
        private void SeedWorld()
        {
            // Deck — face-down stack (zone defaults Hidden).
            _world.AddCard(NewMinion("Bloodfen Raptor", 2, 3, 2, ZoneIdDeck));
            _world.AddCard(NewMinion("Chillwind Yeti",  4, 4, 5, ZoneIdDeck));
            _world.AddCard(NewSpell ("Fireball",        4, "Deal 6 damage.", ZoneIdDeck));
            _world.AddCard(NewSpell ("Healing Touch",   3, "Restore 8 health.", ZoneIdDeck));
            _world.AddCard(NewMinion("Argent Squire",   1, 1, 1, ZoneIdDeck));
            _world.AddCard(NewSpell ("Polymorph",       4, "Transform a minion into a 1/1 Sheep.", ZoneIdDeck));
            _world.AddCard(NewMinion("Tomb Tender",     2, 1, 3, ZoneIdDeck));

            // Graveyard — visible stack of used cards.
            _world.AddCard(NewMinion("Fallen Imp",      1, 2, 1, ZoneIdGraveyard));
            _world.AddCard(NewSpell ("Echoing Spell",   2, "Long since cast.", ZoneIdGraveyard));
            _world.AddCard(NewMinion("Skeleton",        1, 1, 1, ZoneIdGraveyard));

            // Hand + Play Field start empty.
        }

        // --- Entity stub factories --------------------------------------

        private static CardEntity NewMinion(string name, int cost, int atk, int hp, int zoneId) =>
            new()
            {
                Name = name, Kind = CardKind.Minion, Cost = cost,
                Attack = atk, Health = hp, ZoneId = zoneId,
            };

        private static CardEntity NewSpell(string name, int cost, string text, int zoneId) =>
            new()
            {
                Name = name, Kind = CardKind.Spell, Cost = cost,
                BodyText = text, ZoneId = zoneId,
            };
    }
}
