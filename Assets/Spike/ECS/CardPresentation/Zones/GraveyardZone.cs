#nullable enable
namespace Spike.CardPresentation
{
    /// <summary>
    /// Graveyard — face-up stack. Symmetric to <see cref="DeckZone"/> but
    /// cards stay revealed (you can see what's in the graveyard). Top is
    /// draggable, can receive drops.
    /// </summary>
    public sealed class GraveyardZone : CardZone
    {
        private void Reset()
        {
            _layout      = ZoneLayout.Stack;
            _visibility  = ZoneVisibility.Visible;
            _anchoring   = ZoneAnchoring.AnchorBack;
            _draggable   = true;
            _flippable   = false;
            _droppable   = true;
            _ghostAnchor = false;
            _maxCards    = -1;
            _label       = "Graveyard";
        }
    }
}
