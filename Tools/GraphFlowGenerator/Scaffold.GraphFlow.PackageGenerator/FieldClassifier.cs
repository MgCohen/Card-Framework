using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Scaffold.GraphFlow.PackageGenerator
{
    /// <summary>
    /// Mirrors <c>Scaffold.GraphFlow.PortConvention</c>. Kept as ints in the model so we don't take a
    /// hard reference to the attributes assembly from the generator's discovery layer.
    /// </summary>
    internal static class PortConvention
    {
        internal const int CommandResultPair = 0;
        internal const int AttributedFields = 1;
        internal const int MutableInReadOnlyOut = 2;
        internal const int AllFieldsIn = 3;
    }

    /// <summary>
    /// Splits a payload's public instance fields into input / output buckets per the package's
    /// declared <see cref="PortConvention"/>. Mode 2 (command/result pair) bypasses this — the
    /// command type's fields are always inputs and the result type's fields are always outputs.
    /// </summary>
    internal static class FieldClassifier
    {
        internal static (List<IFieldSymbol> inputs, List<IFieldSymbol> outputs) Classify(
            INamedTypeSymbol payload,
            int convention,
            INamedTypeSymbol? inAttr,
            INamedTypeSymbol? outAttr)
        {
            var ins = new List<IFieldSymbol>();
            var outs = new List<IFieldSymbol>();
            foreach (var f in PayloadDiscovery.InstanceFields(payload))
            {
                if (convention == PortConvention.AttributedFields)
                {
                    if (inAttr != null && HasAttribute(f, inAttr))
                    {
                        ins.Add(f);
                    }

                    if (outAttr != null && HasAttribute(f, outAttr))
                    {
                        outs.Add(f);
                    }

                    continue;
                }

                // AllFieldsIn (and CommandResultPair / MutableInReadOnlyOut for non-pair single-payload action emit) → all fields are inputs.
                ins.Add(f);
            }

            return (ins, outs);
        }

        static bool HasAttribute(IFieldSymbol field, INamedTypeSymbol attrType)
        {
            foreach (var a in field.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(a.AttributeClass, attrType))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
