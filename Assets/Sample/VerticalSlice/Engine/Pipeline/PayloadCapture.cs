#nullable enable
using System.Collections.Generic;
using System.Reflection;

namespace VerticalSlice.Engine.Pipeline
{
    /// <summary>
    /// Reflects the public instance properties of a runnable (effect or
    /// command) into a <c>{name → value}</c> dict for the BlockStart's
    /// wire payload. Entity references stay as instances — wire
    /// serialization (when added later) will swap them for ids.
    /// </summary>
    internal static class PayloadCapture
    {
        public static IReadOnlyDictionary<string, object?> Of(object source)
        {
            var dict = new Dictionary<string, object?>();
            var props = source.GetType()
                              .GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var p in props)
            {
                if (!p.CanRead) continue;
                try { dict[p.Name] = p.GetValue(source); }
                catch { dict[p.Name] = "<unreadable>"; }
            }
            return dict;
        }
    }
}
