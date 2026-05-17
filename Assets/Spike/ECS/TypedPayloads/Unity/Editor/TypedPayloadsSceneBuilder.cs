#nullable enable
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TypedPayloads.UnityScene.Editor
{
    /// <summary>
    /// One-shot scene builder for the typed-payloads spike. Run from the
    /// menu (<c>Tools → TypedPayloads → Build Sample Scene</c>) or via
    /// batch mode with
    /// <c>-executeMethod TypedPayloads.UnityScene.Editor.TypedPayloadsSceneBuilder.Build</c>.
    /// Overwrites the scene if it already exists — safe to re-run.
    /// </summary>
    public static class TypedPayloadsSceneBuilder
    {
        private const string ScenePath = "Assets/Spike/ECS/TypedPayloads/Unity/Sample.unity";

        [MenuItem("Tools/TypedPayloads/Build Sample Scene")]
        public static void Build()
        {
            var dir = Path.GetDirectoryName(ScenePath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var go = new GameObject("TypedPayloadsBootstrap");
            go.AddComponent<TypedPayloadsBootstrap>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"TypedPayloads: wrote {ScenePath}");
        }
    }
}
