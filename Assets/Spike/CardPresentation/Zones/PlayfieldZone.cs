#nullable enable
namespace Spike.CardPresentation
{
    /// <summary>
    /// Play field — accepts dropped cards face-up, holds up to a fixed
    /// number, and cards on it can't be picked up again (configuration
    /// demo of <c>Draggable=false</c> + capacity limit). A real game's
    /// "battlefield" or "in-play" area.
    /// </summary>
    public sealed class PlayfieldZone : CardZone
    {
        private void Reset()
        {
            _layout      = ZoneLayout.Spread;
            _visibility  = ZoneVisibility.Visible;
            _anchoring   = ZoneAnchoring.AnchorBack;
            _draggable   = false;     // one-way destination — drop only
            _flippable   = false;
            _droppable   = true;
            _ghostAnchor = false;
            _maxCards    = 3;          // demo: capacity rule via CanAccept
            _label       = "Play Field (max 3)";
        }
    }
}
