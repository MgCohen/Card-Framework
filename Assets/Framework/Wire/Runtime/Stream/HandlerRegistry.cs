using System;
using System.Collections.Generic;

namespace Framework.Wire
{
    // Builds a Type → handlers dispatch table once from the DI-collected handler list.
    // Subscribes to EventStream.OnAppended in the constructor — no separate init step.
    // Multiple handlers per event type allowed (logging/metrics future-proofing).
    public sealed class HandlerRegistry
    {
        readonly Dictionary<Type, List<IWireEventHandler>> _byType = new();

        public HandlerRegistry(EventStream stream, IReadOnlyList<IWireEventHandler> handlers)
        {
            for (var i = 0; i < handlers.Count; i++)
            {
                var h = handlers[i];
                if (!_byType.TryGetValue(h.EventType, out var list))
                    _byType[h.EventType] = list = new List<IWireEventHandler>();
                list.Add(h);
            }
            stream.OnAppended += Dispatch;
        }

        public void Dispatch(WireEvent ev)
        {
            if (!_byType.TryGetValue(ev.GetType(), out var list)) return;
            for (var i = 0; i < list.Count; i++) list[i].Apply(ev);
        }
    }
}
