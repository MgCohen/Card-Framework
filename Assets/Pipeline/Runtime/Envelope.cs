namespace Framework.Pipeline
{
    public abstract class Envelope
    {
        protected Envelope(MessageId id, MessageId parentId)
        {
            Id = id;
            ParentId = parentId;
        }

        public MessageId Id { get; }
        public MessageId ParentId { get; }
        public EnvelopeState State { get; private set; } = EnvelopeState.Listed;
        public string Reason { get; private set; }
        public object Result { get; private set; }
        public Message Message => GetMessage();

        protected abstract Message GetMessage();

        // Sole writer: the pipeline run.
        internal void Start() => State = EnvelopeState.Running;

        internal void SetResult(object result) => Result = result;

        internal void Accept(object result)
        {
            State = EnvelopeState.Accepted;
            Result = result;
        }

        internal void Reject(string reason)
        {
            State = EnvelopeState.Rejected;
            Reason = reason;
        }

        internal void Fault() => State = EnvelopeState.Faulted;
    }

    public sealed class Envelope<TMessage> : Envelope where TMessage : Message
    {
        internal Envelope(MessageId id, TMessage message, MessageId parentId = null) : base(id, parentId)
        {
            Message = message;
        }

        public new TMessage Message { get; }

        protected override Message GetMessage() => Message;
    }
}
