#nullable enable
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TagAuthoringPrototype.Authoring;
using TagAuthoringPrototype.Demo;

namespace TagAuthoringPrototype.Editor
{
    /// <summary>
    /// One-shot builder. Run from the menu (or batch via
    /// <c>-executeMethod TagAuthoringPrototype.Editor.TagAuthoringSceneBuilder.Build</c>).
    /// Authors a set of sample <see cref="TagDefinition"/> assets in
    /// <c>Demo/_Generated/</c>, builds a scene with a
    /// <see cref="TagAuthoringDemo"/> component, and wires the assets onto
    /// the component fields.
    ///
    /// <para>Idempotent — re-run after editing the prototype and the
    /// scene rebuilds against the same asset GUIDs.</para>
    /// </summary>
    public static class TagAuthoringSceneBuilder
    {
        const string Root         = "Assets/Spike/ECS/TagAuthoringPrototype";
        const string GeneratedDir = Root + "/Demo/_Generated";
        const string ScenePath    = Root + "/Demo/Sample.unity";

        [MenuItem("Tools/TagAuthoringPrototype/Build Sample Scene")]
        public static void Build()
        {
            EnsureDirectory(GeneratedDir);

            var health  = LoadOrCreate<IntTag>(   "HealthTag",  1001, "health",  "Health",   new Color(0.85f, 0.20f, 0.20f));
            var mana    = LoadOrCreate<IntTag>(   "ManaTag",    1002, "mana",    "Mana",     new Color(0.20f, 0.45f, 0.85f));
            var onFire  = LoadOrCreate<BoolTag>(  "OnFireTag",  1003, "onFire",  "On Fire",  new Color(0.95f, 0.50f, 0.10f));
            var element = LoadOrCreate<StringTag>("ElementTag", 1004, "element", "Element",  new Color(0.65f, 0.65f, 0.65f));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var go = new GameObject("TagAuthoringDemo");
            var demo = go.AddComponent<TagAuthoringDemo>();

            var so = new SerializedObject(demo);
            so.FindProperty("healthTag" ).objectReferenceValue = health;
            so.FindProperty("manaTag"   ).objectReferenceValue = mana;
            so.FindProperty("onFireTag" ).objectReferenceValue = onFire;
            so.FindProperty("elementTag").objectReferenceValue = element;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log($"TagAuthoringPrototype: scene built at {ScenePath}");
        }

        // --- Asset authoring helpers --------------------------------------

        static T LoadOrCreate<T>(string assetName, int slot, string id, string label, Color color)
            where T : TagDefinition
        {
            var path = $"{GeneratedDir}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);

            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(existing, path);
            }

            // Populate authored fields via SerializedObject — works on both
            // freshly-created and pre-existing assets, so re-running the
            // builder refreshes labels/colors without leaving orphaned data.
            var so = new SerializedObject(existing);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("slot").intValue = slot;

            var pres = so.FindProperty("presentation");
            pres.FindPropertyRelative("label").stringValue = label;
            pres.FindPropertyRelative("color").colorValue = color;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(existing);
            return existing;
        }

        static void EnsureDirectory(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            var parent = Path.GetDirectoryName(assetPath)!.Replace('\\', '/');
            var leaf   = Path.GetFileName(assetPath);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
