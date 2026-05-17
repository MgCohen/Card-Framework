#nullable enable
using UnityEngine;

namespace TagAuthoringPrototype.Authoring
{
    [CreateAssetMenu(menuName = "Tags/Float Tag", fileName = "FloatTag", order = 2)]
    public sealed class FloatTag : TagDefinition<float> { }
}
