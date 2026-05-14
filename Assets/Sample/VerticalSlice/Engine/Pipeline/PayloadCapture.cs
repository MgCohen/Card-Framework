#nullable enable
using System.Collections.Generic;
using System.Reflection;
using VerticalSlice.Engine.Entities;

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

        /// <summary>
        /// Convention-based source extraction: if the runnable has a public
        /// instance property named "Source" of an IEntity-compatible type,
        /// return its value. Otherwise null. Lets the engine populate
        /// <c>BlockStartEvent.Source</c> without authors implementing an
        /// interface — they just keep using a public Source field.
        /// </summary>
        public static IEntity? ExtractSource(object source)
        {
            var prop = source.GetType().GetProperty(
                "Source", BindingFlags.Public | BindingFlags.Instance);
            if (prop == null || !prop.CanRead) return null;
            if (!typeof(IEntity).IsAssignableFrom(prop.PropertyType)) return null;
            try { return prop.GetValue(source) as IEntity; }
            catch { return null; }
        }
    }
}
