using System;

namespace Scaffold.GraphFlow
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class GraphMenuAttribute : Attribute
    {
        public GraphMenuAttribute(string path)
        {
            Path = path;
        }

        public string Path { get; }
    }
}
