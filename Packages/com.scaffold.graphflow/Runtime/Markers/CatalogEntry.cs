#nullable enable
using System;
using System.Collections.Generic;

namespace Scaffold.GraphFlow
{
    public sealed class CatalogEntry
    {
        public CatalogKind             Kinds          { get; }
        public Type                    Type           { get; }
        public IReadOnlyList<PortMeta> Ports          { get; }
        public Func<RuntimeNode>?      CreateRuntime  { get; }
        public string?                 DefaultLiteral { get; }
        public Type?                   ResultType     { get; }

        public CatalogEntry(
            CatalogKind kinds,
            Type type,
            IReadOnlyList<PortMeta> ports,
            Func<RuntimeNode>? createRuntime = null,
            string? defaultLiteral = null,
            Type? resultType = null)
        {
            Kinds          = kinds;
            Type           = type;
            Ports          = ports;
            CreateRuntime  = createRuntime;
            DefaultLiteral = defaultLiteral;
            ResultType     = resultType;
        }
    }
}
