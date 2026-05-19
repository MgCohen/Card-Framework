using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Editor
{
    /// <summary>Stable editor variable identity. Reads the persisted Unity <c>Hash128</c>
    /// GUID from the IVariable's concrete type. GT variable models inherit <c>Guid</c> from
    /// the <c>Model</c> base class. Hard-fails on reflection drift so baked assets never
    /// silently degrade to rename-fragile name-based ids.</summary>
    static class EditorVariableIdentity
    {
        static readonly Dictionary<Type, PropertyInfo?> s_guidPropByType = new();

        internal static string GetStableGuid(IVariable variable)
        {
            if (variable == null)
                throw new ArgumentNullException(nameof(variable));

            var type = variable.GetType();
            if (!s_guidPropByType.TryGetValue(type, out var guidProp))
            {
                guidProp = type.GetProperty("Guid", BindingFlags.Public | BindingFlags.Instance);
                s_guidPropByType[type] = guidProp;
            }

            if (guidProp == null)
                throw new InvalidOperationException(
                    $"GraphFlow: Guid property not found on {type.FullName} — GT version drift. " +
                    "Update the reflection contract in EditorVariableIdentity.");

            var s = guidProp.GetValue(variable)?.ToString();
            if (string.IsNullOrWhiteSpace(s))
                throw new InvalidOperationException(
                    $"GraphFlow: Guid was empty for variable '{variable.Name}'.");

            return s;
        }
    }
}
