// Polyfill so C# 9 `init` accessors compile against netstandard2.1
// (Unity's target). The C# compiler matches this type by name + namespace;
// it has no runtime body. Once the engine targets net5+ this can go.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
