#nullable enable
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VerticalSlice.UnityScene.Editor
{
    /// <summary>
    /// One-shot scene builder. Run from the menu, or via Unity batch mode with
    /// <c>-executeMethod VerticalSlice.UnityScene.Editor.SliceSceneBuilder.Build</c>.
    /// Overwrites the scene if it already exists — safe to re-run after gutting
    /// the bootstrap.
    /// </summary>
    public static class SliceSceneBuilder
    {
        private const string ScenePath = "Assets/Spike/ECS/VerticalSlice/Unity/Sample.unity";

        [MenuItem("Tools/VerticalSlice/Build Sample Scene")]
        public static void Build()
        {
            var dir = Path.GetDirectoryName(ScenePath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var go = new GameObject("SliceBootstrap");
            go.AddComponent<SliceBootstrap>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"VerticalSlice: wrote {ScenePath}");
        }
    }
}
