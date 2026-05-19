#nullable enable
using UnityEditor;
using UnityEngine;

namespace Scaffold.GraphFlow.Editor
{
    // Runs GraphResultValidator on every GraphAsset that gets (re)imported.
    // Diagnostics surface as Debug.LogWarning entries in the Console, with
    // the asset attached as context so clicking the message pings it in the
    // Project window.
    sealed class GraphResultValidationPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (var path in importedAssets)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GraphAsset>(path);
                if (asset == null) continue;

                foreach (var diag in GraphResultValidator.Validate(asset))
                    Debug.LogWarning(diag.Message, asset);
            }
        }
    }
}
