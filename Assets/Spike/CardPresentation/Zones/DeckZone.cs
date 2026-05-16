#nullable enable
namespace Spike.CardPresentation
{
    /// <summary>
    /// Deck — cards stack face-down. The top card can be drawn (dragged
    /// off); the zone accepts drops onto the top of the pile. Identical
    /// behavior to the graveyard except for the hidden visibility default.
    /// </summary>
    public sealed class DeckZone : CardZone
    {
        private void Reset()
        {
            _layout      = ZoneLayout.Stack;
            _visibility  = ZoneVisibility.Hidden;
            _anchoring   = ZoneAnchoring.AnchorBack;
            _draggable   = true;
            _flippable   = false;
            _droppable   = true;
            _ghostAnchor = false;
            _maxCards    = -1;
            _label       = "Deck";
        }
    }
}
