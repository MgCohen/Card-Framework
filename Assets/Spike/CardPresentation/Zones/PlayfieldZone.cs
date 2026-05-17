#nullable enable
namespace Spike.CardPresentation
{
    /// <summary>
    /// Play field — accepts dropped cards face-up, holds up to a fixed
    /// number. Capacity (<c>MaxCards = 3</c>) is the rule on display here;
    /// what cards <em>do</em> when picked up off the field is the
    /// <see cref="GameSystem"/>'s call (it allows Play → Graveyard, denies
    /// Play → Hand / Deck via its valid-actions allow-list).
    /// </summary>
    public sealed class PlayfieldZone : CardZone
    {
        private void Reset()
        {
            _layout      = ZoneLayout.Spread;
            _visibility  = ZoneVisibility.Visible;
            _anchoring   = ZoneAnchoring.AnchorBack;
            _draggable   = true;       // system decides; zone doesn't veto
            _flippable   = false;
            _droppable   = true;
            _ghostAnchor = false;
            _maxCards    = 3;
            _label       = "Play Field (max 3)";
        }
    }
}
