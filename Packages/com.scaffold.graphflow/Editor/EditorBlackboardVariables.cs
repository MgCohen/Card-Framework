using System;
using System.Collections.Generic;
using Scaffold.Variables;
using Unity.GraphToolkit.Editor;

namespace Scaffold.GraphFlow.Editor
{
    /// <summary>Builds a typed <see cref="VariableDefault"/> from a GT <see cref="IVariable"/>.
    /// Known types (int, float, bool, string, Object) are dispatched directly. Unknown types
    /// are resolved via reflection — any <see cref="BlackboardVariable{T}"/> subclass visible to
    /// the runtime assembly is discovered automatically.</summary>
    static class EditorBlackboardVariables
    {
        static Dictionary<Type, Type>? s_defaultByValueType;

        public static VariableDefault? CreateFor(IVariable variable)
        {
            if (variable == null) return null;
            var t = variable.DataType;
            if (t == null) return null;

            if (t == typeof(int))    { variable.TryGetDefaultValue(out int v);    return new BlackboardInt    { value = v }; }
            if (t == typeof(float))  { variable.TryGetDefaultValue(out float v);  return new BlackboardFloat  { value = v }; }
            if (t == typeof(bool))   { variable.TryGetDefaultValue(out bool v);   return new BlackboardBool   { value = v }; }
            if (t == typeof(string)) { variable.TryGetDefaultValue(out string v); return new BlackboardString { value = v ?? string.Empty }; }
            if (typeof(UnityEngine.Object).IsAssignableFrom(t))
            {
                variable.TryGetDefaultValue(out UnityEngine.Object v);
                return new BlackboardObject { value = v };
            }

            return TryCreateViaReflection(t);
        }

        static VariableDefault? TryCreateViaReflection(Type valueType)
        {
            if (s_defaultByValueType == null)
            {
                s_defaultByValueType = new Dictionary<Type, Type>();
                var baseType = typeof(BlackboardVariable<>);
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch { continue; }

                    foreach (var candidate in types)
                    {
                        if (candidate.IsAbstract || candidate.IsGenericTypeDefinition) continue;
                        var bt = candidate.BaseType;
                        if (bt == null || !bt.IsGenericType || bt.GetGenericTypeDefinition() != baseType) continue;
                        var tArg = bt.GetGenericArguments()[0];
                        s_defaultByValueType.TryAdd(tArg, candidate);
                    }
                }
            }

            if (!s_defaultByValueType.TryGetValue(valueType, out var defaultType))
                return null;

            return Activator.CreateInstance(defaultType) as VariableDefault;
        }
    }
}
