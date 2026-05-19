#nullable enable
using Scaffold.GraphFlow.CardSandbox.Showcase;
using UnityEditor;
using UnityEngine;

namespace Scaffold.GraphFlow.CardSandbox.Editor
{
    static class CreateShowcaseAsset
    {
        const string AssetPath =
            "Assets/Packages/com.scaffold.graphflow/Samples/CardSandbox/Scenes/StrikeWithVariables.asset";

        [MenuItem("Scaffold/Create Variable Showcase Asset")]
        static void Execute()
        {
            var asset = StrikeWithVariables.BuildAsset();
            AssetDatabase.CreateAsset(asset, AssetPath);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
            Debug.Log($"[GraphFlow] Showcase asset saved → {AssetPath}");
        }
    }
}
