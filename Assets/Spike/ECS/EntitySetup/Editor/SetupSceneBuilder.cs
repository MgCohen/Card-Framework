#nullable enable
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Spike.EntitySetup.Editor
{
    /// <summary>
    /// One-shot scene builder. Run from <c>Tools/EntitySetup/Build Sandbox
    /// Scene</c>, or via Unity batch mode with
    /// <c>-executeMethod Spike.EntitySetup.Editor.SetupSceneBuilder.Build</c>.
    /// Overwrites <c>Sandbox.unity</c> if it already exists — safe to
    /// re-run after editing the bootstrap.
    /// </summary>
    public static class SetupSceneBuilder
    {
        private const string ScenePath = "Assets/Spike/ECS/EntitySetup/Sandbox.unity";

        [MenuItem("Tools/EntitySetup/Build Sandbox Scene")]
        public static void Build()
        {
            var dir = Path.GetDirectoryName(ScenePath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var go = new GameObject("EntitySetupBootstrap");
            go.AddComponent<EntitySetupBootstrap>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"EntitySetup: wrote {ScenePath}");
        }
    }
}
