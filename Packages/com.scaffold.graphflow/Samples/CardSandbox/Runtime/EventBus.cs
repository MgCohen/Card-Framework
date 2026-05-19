#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Scaffold.GraphFlow.CardSandbox
{
    /// <summary>
    /// Sample-only event bus (post-M3 phase 3: Timing-aware). Subscribers may filter by
    /// <see cref="Timing"/> at subscription time; publishers stamp each delivery with the phase
    /// they're emitting in. Sequential await on Publish; subscribers run in registration order.
    /// <para>NOT part of the package — the framework knows nothing about how triggers are
    /// dispatched. Production hosts pick their own bus shape.</para>
    /// </summary>
    public sealed class EventBus
    {
        readonly Dictionary<Type, List<(Func<object, Task> Handler, Timing? Filter)>> _subs = new();

        public void Subscribe<T>(Func<T, Task> handler, Timing? timing = null) where T : class
        {
            if (!_subs.TryGetValue(typeof(T), out var list))
                _subs[typeof(T)] = list = new List<(Func<object, Task>, Timing?)>();
            list.Add((o => handler((T)o), timing));
        }

        public async Task Publish<T>(T evt, Timing timing) where T : class
        {
            if (!_subs.TryGetValue(typeof(T), out var list)) return;
            foreach (var (h, filter) in list)
            {
                if (filter == null || filter == timing)
                    await h(evt);
            }
        }
    }
}
