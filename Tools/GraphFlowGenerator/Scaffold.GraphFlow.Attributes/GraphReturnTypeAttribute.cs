using System;

namespace Scaffold.GraphFlow
{
    /// <summary>
    /// Marks a payload type as available in <c>Return&lt;T&gt;</c>'s edit-time picker. Combine with
    /// the catalog's hardcoded primitives (int / bool / string / float / Unit) to control which
    /// types appear in a package's Return dropdown.
    /// <para>Discovery is asm-scoped to the package's runtime asm — same rule used for events and
    /// commands. Attributes complement structural rules (inheritance / interface markers); the
    /// catalog's <c>AdditionalCatalogEntries</c> field on <c>[GraphPackage]</c> (future) lets
    /// consumers inject types not reachable through structural discovery.</para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class GraphReturnTypeAttribute : Attribute
    {
    }
}
