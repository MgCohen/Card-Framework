#nullable enable
using UnityEngine;

namespace TagAuthoringPrototype.Authoring
{
    [CreateAssetMenu(menuName = "Tags/String Tag", fileName = "StringTag", order = 3)]
    public sealed class StringTag : TagDefinition<string> { }
}
