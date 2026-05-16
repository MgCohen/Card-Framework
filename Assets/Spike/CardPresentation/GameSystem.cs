#nullable enable
namespace Spike.CardPresentation
{
    /// <summary>
    /// Intent processor — the middle layer between view-emitted requests
    /// and world mutations. Receives intents (currently just MoveCard),
    /// validates them, applies to the <see cref="World"/>.
    ///
    /// <para>In a real game this is where rule logic lives: turn checks,
    /// cost validation, triggered effects, etc. Here it just forwards.
    /// The shape is the seam — when the engine arrives, swap this
    /// implementation in.</para>
    /// </summary>
    public sealed class GameSystem
    {
        private readonly World _world;

        public GameSystem(World world) { _world = world; }

        /// <summary>
        /// "Move card X to zone Y." Currently trusts the view's
        /// <c>CanAccept</c> gate; a real rule layer would re-check here
        /// against game state (turn, cost, restrictions).
        /// </summary>
        public void MoveCard(int cardId, int targetZoneId)
        {
            _world.SetCardZone(cardId, targetZoneId);
        }
    }
}
