using System;

namespace Scaffold.GraphFlow
{
    [Flags]
    public enum CatalogKind
    {
        None    = 0,
        Event   = 1 << 0,
        Return  = 1 << 1,
        Command = 1 << 2,
        Entry    = 1 << 3,
        Variable = 1 << 4,
    }
}
