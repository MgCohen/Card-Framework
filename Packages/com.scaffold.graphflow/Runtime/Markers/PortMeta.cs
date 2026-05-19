using System;

namespace Scaffold.GraphFlow
{
    public enum PortDirection
    {
        Input,
        Output,
    }

    public readonly struct PortMeta
    {
        public string        Name      { get; }
        public Type          Type      { get; }
        public PortDirection Direction { get; }

        public PortMeta(string name, Type type, PortDirection direction)
        {
            Name      = name;
            Type      = type;
            Direction = direction;
        }
    }
}
