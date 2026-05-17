#nullable enable
using UnityEngine;

namespace TagAuthoringPrototype.Authoring
{
    [CreateAssetMenu(menuName = "Tags/Int Tag", fileName = "IntTag", order = 0)]
    public sealed class IntTag : TagDefinition<int> { }
}
