#nullable enable
using Scaffold.GraphFlow.CardSandbox.Editor.GToolkit;
using Scaffold.GraphFlow.CardSandbox.Generated;
using Scaffold.GraphFlow.Editor;
using UnityEditor.AssetImporters;

namespace Scaffold.GraphFlow.CardSandbox.Editor
{
    [ScriptedImporter(2, CardEffectGraph.AssetExtension)]
    public sealed class CardEffectGraphImporter : GraphAssetImporterBase<CardEffectGraph, CardEffectRunner, CardEffectGraphAsset>
    {
    }
}
