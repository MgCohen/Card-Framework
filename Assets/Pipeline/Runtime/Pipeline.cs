using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Framework.RecordIds;
using Framework.TypeRouting;

namespace Framework.Pipeline
{
    public abstract class Pipeline<TMessage, TScope, TView> where TMessage : Message
    {
        readonly TypeRouter<Handler<TMessage, TScope>> _handlers;
        readonly TypeRouter<Validator<TMessage, TView>> _validators;
        readonly IdAllocator<MessageId> _ids;
        readonly List<Envelope<TMessage>> _items = new();

        protected Pipeline(PipelineServices<TMessage, TScope, TView> services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            _handlers = services.Handlers;
            _validators = services.Validators;
            _ids = services.Ids;
        }

        public IReadOnlyList<Envelope<TMessage>> Items => _items;

        public Envelope<TMessage> Add(TMessage message)
        {
            var e = new Envelope<TMessage>(_ids.Next(), message);
            _items.Add(e);
            return e;
        }

        public bool Remove(MessageId id)
        {
            for (var i = 0; i < _items.Count; i++)
            {
                if (_items[i].Id != id) continue;
                _items.RemoveAt(i);
                return true;
            }
            return false;
        }

        public bool TryPeek(out Envelope<TMessage> envelope)
        {
            envelope = SelectNext(_items);
            return envelope != null;
        }

        public bool TryTake(out Envelope<TMessage> envelope)
        {
            envelope = SelectNext(_items);
            if (envelope == null) return false;
            _items.Remove(envelope);
            return true;
        }

        public virtual Task Run() => RunOne();

        protected Task<Envelope<TMessage>> RunOne()
        {
            if (!TryTake(out var e)) return Task.FromResult<Envelope<TMessage>>(null);
            return Execute(e);
        }

        protected Task<Envelope<TMessage>> RunInline(Envelope parent, TMessage message)
            => Execute(new Envelope<TMessage>(_ids.Next(), message, parent.Id));

        protected virtual Envelope<TMessage> SelectNext(IReadOnlyList<Envelope<TMessage>> items)
            => items.Count > 0 ? items[0] : null;

        /// <summary>Makes the scope for one run. The scope must be a <see cref="PipelineScope"/>; if not, the run faults.</summary>
        protected abstract TScope CreateScope(Envelope<TMessage> e);

        /// <summary>Makes the read-only view that the validator of the message reads. Not called when the message has no validator.</summary>
        protected abstract TView CreateView(Envelope<TMessage> e);

        protected virtual bool Validate(Envelope<TMessage> e, out string reason)
        {
            reason = null;
            return !_validators.TryGet(e.Message.GetType(), out var validator)
                || validator.Dispatch(e.Message, CreateView(e), out reason);
        }

        protected virtual Task Before(Envelope<TMessage> e, TScope scope) => Task.CompletedTask;

        protected virtual Task<object> Handle(Envelope<TMessage> e, TScope scope)
            => _handlers.Get(e.Message.GetType()).Dispatch(e.Message, scope);

        protected virtual Task After(Envelope<TMessage> e, TScope scope) => Task.CompletedTask;

        // Not virtual: a subclass changes a step, never the order of the steps.
        protected async Task<Envelope<TMessage>> Execute(Envelope<TMessage> e)
        {
            PipelineScope closable = null;
            e.Start();
            try
            {
                if (!Validate(e, out var reason))
                {
                    e.Reject(reason);
                    return e;
                }

                var scope = CreateScope(e);
                closable = scope as PipelineScope
                    ?? throw new InvalidOperationException($"{GetType().Name}.CreateScope must return a PipelineScope.");
                await Before(e, scope);
                var result = await Handle(e, scope);
                await After(e, scope);
                e.Accept(result);
                return e;
            }
            catch
            {
                e.Fault();
                throw;
            }
            finally
            {
                closable?.Close();
            }
        }
    }
}
