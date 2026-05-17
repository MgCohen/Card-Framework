// Polyfill so C# 9 `init` accessors / records compile against
// netstandard2.1 (Unity's target). The C# compiler matches this type
// by name + namespace; it has no runtime body. Once Unity targets
// net5+ this can go.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
