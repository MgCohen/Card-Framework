#nullable enable

namespace Scaffold.GraphFlow.Editor
{
    /// <summary>
    /// Kept as a base class only — no [CustomEditor] attribute. A custom editor for the
    /// asset type overrides Unity's ScriptedImporter inspector (hiding import settings),
    /// so we rely on [HideInInspector] on GraphAsset fields instead. With proper
    /// ScriptedImporter GUID resolution in the meta file, Unity shows the combined
    /// import-settings + imported-object view (matching official GT sample behavior).
    /// </summary>
    public class GraphAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI() => DrawDefaultInspector();
    }
}
