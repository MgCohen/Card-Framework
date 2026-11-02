using System;
using Framework.RecordIds;
using Framework.TypeRouting;

namespace Framework.Pipeline
{
    public sealed class PipelineServices<TMessage, TScope, TView> where TMessage : Message
    {
        public PipelineServices(TypeRouter<Handler<TMessage, TScope>> handlers,
                                TypeRouter<Validator<TMessage, TView>> validators,
                                IdAllocator<MessageId> ids)
        {
            Handlers = handlers ?? throw new ArgumentNullException(nameof(handlers));
            Validators = validators ?? throw new ArgumentNullException(nameof(validators));
            Ids = ids ?? throw new ArgumentNullException(nameof(ids));
        }

        public TypeRouter<Handler<TMessage, TScope>> Handlers { get; }
        public TypeRouter<Validator<TMessage, TView>> Validators { get; }
        public IdAllocator<MessageId> Ids { get; }
    }
}
