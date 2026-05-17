#nullable enable
namespace VerticalSlice.Engine.Stream
{
    /// <summary>
    /// Base for typed option wire events. Each request opens a
    /// <see cref="BlockKind.Request"/> block; inside the block, one
    /// concrete <see cref="OptionEvent"/> subclass is emitted per
    /// option declared.
    ///
    /// The wire form is purely data — entity ids, scalar values, the
    /// option's ordinal index. No labels (display strings live on the
    /// client). No predicates / Funcs. Replay-friendly.
    ///
    /// Future option kinds drop in alongside without changing the
    /// existing ones — e.g. <c>TargetOptionEvent</c>,
    /// <c>NumberOptionEvent</c>, <c>ConfirmOptionEvent</c>.
    /// </summary>
    public abstract class OptionEvent : WireEvent
    {
        public int Index { get; }
        protected OptionEvent(int index) { Index = index; }
    }

    /// <summary>
    /// "Pick this option." Generic shape: optional source, optional
    /// target, optional scalar value. Covers Choose-One ("deal 6 to
    /// target"), Play-card-with-target, and most pick-this cases.
    /// </summary>
    public sealed class PickOptionEvent : OptionEvent
    {
        public int? SourceId { get; }
        public int? TargetId { get; }
        public int? Value { get; }

        public PickOptionEvent(int index, int? source = null, int? target = null, int? value = null)
            : base(index)
        {
            SourceId = source;
            TargetId = target;
            Value = value;
        }

        public override string Kind => "PickOption";
        public override string ToString()
        {
            var src = SourceId.HasValue ? $", src=#{SourceId.Value}" : "";
            var tgt = TargetId.HasValue ? $", tgt=#{TargetId.Value}" : "";
            var val = Value.HasValue   ? $", val={Value.Value}"     : "";
            return $"PickOption(idx={Index}{src}{tgt}{val})";
        }
    }

    /// <summary>
    /// The cancel / dismiss option. Hearthstone-style "softer" affordance
    /// — UI renders distinctly (gray button, ESC keybind). Picking this
    /// option is recorded on the wire like any other; the Request's
    /// Resolve handler is responsible for short-circuiting.
    /// </summary>
    public sealed class CancelOptionEvent : OptionEvent
    {
        public CancelOptionEvent(int index) : base(index) { }
        public override string Kind => "CancelOption";
        public override string ToString() => $"CancelOption(idx={Index})";
    }

    /// <summary>
    /// The player's response to an open request. Emitted by
    /// <c>RequestsService.Respond</c>; the engine's Resolve handler
    /// runs immediately after.
    /// </summary>
    public sealed class ResponseEvent : WireEvent
    {
        public int RequestId { get; }
        public int Picked { get; }

        public ResponseEvent(int requestId, int picked)
        {
            RequestId = requestId;
            Picked = picked;
        }

        public override string Kind => "Response";
        public override string ToString() => $"Response(reqId={RequestId}, picked={Picked})";
    }
}
