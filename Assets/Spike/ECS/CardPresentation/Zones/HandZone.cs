#nullable enable
namespace Spike.CardPresentation
{
    /// <summary>
    /// Standard hand row: spread layout, draggable, anchors back. Pure
    /// preset — all behavior lives on <see cref="CardZone"/>; this class
    /// just sets defaults via Unity's <c>Reset()</c> callback so the prefab
    /// gets the right configuration when the component is first added.
    /// </summary>
    public sealed class HandZone : CardZone
    {
        private void Reset()
        {
            _layout      = ZoneLayout.Spread;
            _visibility  = ZoneVisibility.Visible;
            _anchoring   = ZoneAnchoring.AnchorBack;
            _draggable   = true;
            _flippable   = false;
            _droppable   = true;     // hand will receive cards eventually
            _ghostAnchor = true;     // hand preserves slot during drag
            _maxCards    = -1;
            _label       = "Hand";
        }
    }
}
