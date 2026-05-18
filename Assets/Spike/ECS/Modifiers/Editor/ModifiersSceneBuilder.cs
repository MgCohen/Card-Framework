#nullable enable
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Spike.Modifiers.Editor
{
    public static class ModifiersSceneBuilder
    {
        private const string ScenePath = "Assets/Spike/ECS/Modifiers/Sandbox.unity";

        [MenuItem("Tools/Modifiers/Build Sandbox Scene")]
        public static void Build()
        {
            var dir = Path.GetDirectoryName(ScenePath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var go = new GameObject("ModifiersBootstrap");
            go.AddComponent<ModifiersBootstrap>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"Modifiers: wrote {ScenePath}");
        }
    }
}
