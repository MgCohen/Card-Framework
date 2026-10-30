using System.Collections.Generic;
using Scaffold.AutoPacker;

namespace Framework.Wire.Tests
{
    public sealed class SimplePackingHandler : IPackingHandler
    {
        readonly Dictionary<string, int> _stringToId = new();
        readonly Dictionary<int, string> _idToString = new();
        int _next = 1;

        public TTarget Resolve<TSource, TTarget>(TSource source)
        {
            if (source is string s && typeof(TTarget) == typeof(int))
            {
                if (!_stringToId.TryGetValue(s, out var id))
                {
                    id = _next++;
                    _stringToId[s] = id;
                    _idToString[id] = s;
                }
                return (TTarget)(object)id;
            }
            if (source is int i && typeof(TTarget) == typeof(string))
            {
                _idToString.TryGetValue(i, out var str);
                return (TTarget)(object)str;
            }
            if (source is TTarget target) return target;
            return default;
        }
    }
}
