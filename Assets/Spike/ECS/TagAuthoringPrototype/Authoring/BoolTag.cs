#nullable enable
using UnityEngine;

namespace TagAuthoringPrototype.Authoring
{
    [CreateAssetMenu(menuName = "Tags/Bool Tag", fileName = "BoolTag", order = 1)]
    public sealed class BoolTag : TagDefinition<bool> { }
}
