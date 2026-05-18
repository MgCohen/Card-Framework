#nullable enable
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using StateMachine.UnityScene;

namespace StateMachine.UnityScene.Editor
{
    /// <summary>
    /// One-shot scene builder. Run from <c>Tools/StateMachine/Build Sample
    /// Scene</c>, or via Unity batch mode with
    /// <c>-executeMethod StateMachine.UnityScene.Editor.StateMachineSceneBuilder.Build</c>.
    /// Overwrites <c>Sample.unity</c> if it already exists.
    /// </summary>
    public static class StateMachineSceneBuilder
    {
        private const string ScenePath = "Assets/Spike/ECS/StateMachine/Unity/Sample.unity";

        [MenuItem("Tools/StateMachine/Build Sample Scene")]
        public static void Build()
        {
            var dir = Path.GetDirectoryName(ScenePath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var go = new GameObject("StateMachineBootstrap");
            go.AddComponent<StateMachineBootstrap>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"StateMachine: wrote {ScenePath}");
        }
    }
}
