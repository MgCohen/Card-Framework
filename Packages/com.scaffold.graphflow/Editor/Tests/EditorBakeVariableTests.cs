#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Scaffold.GraphFlow.CardSandbox;
using Scaffold.GraphFlow.CardSandbox.Editor.GToolkit;
using Scaffold.GraphFlow.CardSandbox.Generated;
using Scaffold.GraphFlow.Editor;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Scaffold.GraphFlow.EditorTests
{
    public sealed class EditorBakeVariableTests
    {
        const string TempDir = "Assets/_EditorTestTemp";
        string? _assetPath;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempDir))
                AssetDatabase.CreateFolder("Assets", "_EditorTestTemp");
        }

        [TearDown]
        public void TearDown()
        {
            if (_assetPath != null && File.Exists(_assetPath))
                AssetDatabase.DeleteAsset(_assetPath);

            if (AssetDatabase.IsValidFolder(TempDir))
                AssetDatabase.DeleteAsset(TempDir);
        }

        static object GetImplementation(Graph graph)
        {
            var field = typeof(Graph).GetField("m_Implementation",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "Could not find m_Implementation on Graph.");
            var impl = field!.GetValue(graph);
            Assert.IsNotNull(impl, "m_Implementation is null — was the graph loaded via GraphDatabase?");
            return impl!;
        }

        static IVariable CreateVariable(object impl, string name, Type valueType, object? defaultValue = null)
        {
            var method = impl.GetType().GetMethod("CreateVariable", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(method, $"CreateVariable method not found on {impl.GetType().FullName}.");
            return (IVariable)method!.Invoke(impl, new object?[] { name, valueType, defaultValue, VariableKind.Local })!;
        }

        static void AddConstantNode(object impl)
        {
            var method = impl.GetType().GetMethod("CreateConstantNode",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(string), typeof(Vector2), typeof(Type), typeof(object) },
                null);
            Assert.IsNotNull(method, $"CreateConstantNode not found on {impl.GetType().FullName}.");
            method!.Invoke(impl, new object?[] { "_dummy", Vector2.zero, typeof(int), 0 });
        }

        CardEffectGraph CreateTestGraph(string fileName)
        {
            _assetPath = $"{TempDir}/{fileName}.card";
            var graph = GraphDatabase.CreateGraph<CardEffectGraph>(_assetPath);
            AddConstantNode(GetImplementation(graph));
            return graph;
        }

        // Item 7: Round-trip bake test — author variables in a real GT graph,
        // bake through GraphBakerCore, and assert the RuntimeVariable[] output.
        [Test]
        public void BakeVariables_RoundTrip_ProducesCorrectRuntimeVariables()
        {
            var graph = CreateTestGraph("BakeVarRoundTrip");
            var impl = GetImplementation(graph);

            CreateVariable(impl, "health", typeof(int), 100);
            CreateVariable(impl, "speed", typeof(float), 3.5f);
            CreateVariable(impl, "playerName", typeof(string), "hero");

            var result = GraphBakerCore.Bake<CardEffectRunner, CardEffectGraphAsset>(
                graph, null, CardEffectGraphRegistry.Instance);

            Assert.IsFalse(result.HasErrors, $"Bake had errors: {string.Join("; ", result.Diagnostics)}");
            Assert.IsNotNull(result.Asset);

            var vars = result.Asset!.variables;
            Assert.AreEqual(3, vars.Count);

            var health = vars.FirstOrDefault(v => v.name == "health");
            Assert.IsNotNull(health, "Expected 'health' variable in baked output.");
            Assert.IsInstanceOf<BlackboardInt>(health!.defaultValue);
            Assert.AreEqual(100, ((BlackboardInt)health.defaultValue).value);

            var speed = vars.FirstOrDefault(v => v.name == "speed");
            Assert.IsNotNull(speed, "Expected 'speed' variable in baked output.");
            Assert.IsInstanceOf<BlackboardFloat>(speed!.defaultValue);
            Assert.AreEqual(3.5f, ((BlackboardFloat)speed.defaultValue).value);

            var playerName = vars.FirstOrDefault(v => v.name == "playerName");
            Assert.IsNotNull(playerName, "Expected 'playerName' variable in baked output.");
            Assert.IsInstanceOf<BlackboardString>(playerName!.defaultValue);
            Assert.AreEqual("hero", ((BlackboardString)playerName.defaultValue).value);
        }

        // Item 7 (cont.): Verify EditorVariableIdentity produces stable GUIDs —
        // baking the same graph twice yields the same variable ids.
        [Test]
        public void BakeVariables_StableIdentity_AcrossMultipleBakes()
        {
            var graph = CreateTestGraph("BakeVarIdentity");
            var impl = GetImplementation(graph);
            CreateVariable(impl, "score", typeof(int), 0);

            var bake1 = GraphBakerCore.Bake<CardEffectRunner, CardEffectGraphAsset>(
                graph, null, CardEffectGraphRegistry.Instance);
            Assert.IsFalse(bake1.HasErrors);

            var bake2 = GraphBakerCore.Bake<CardEffectRunner, CardEffectGraphAsset>(
                graph, bake1.Asset, CardEffectGraphRegistry.Instance);
            Assert.IsFalse(bake2.HasErrors);

            Assert.AreEqual(1, bake1.Asset!.variables.Count);
            Assert.AreEqual(1, bake2.Asset!.variables.Count);
            Assert.AreEqual(bake1.Asset.variables[0].id, bake2.Asset.variables[0].id);
        }

        // Item 7 (cont.): Verify the typeName/defaultValue.ValueType assertion
        // catches mismatches (Item 4 defense).
        [Test]
        public void BakeVariables_TypeNameMatchesDefaultValueType()
        {
            var graph = CreateTestGraph("BakeVarTypeName");
            var impl = GetImplementation(graph);
            CreateVariable(impl, "flag", typeof(bool), true);

            var result = GraphBakerCore.Bake<CardEffectRunner, CardEffectGraphAsset>(
                graph, null, CardEffectGraphRegistry.Instance);
            Assert.IsFalse(result.HasErrors);

            var flag = result.Asset!.variables.FirstOrDefault(v => v.name == "flag");
            Assert.IsNotNull(flag);
            Assert.AreEqual(typeof(bool).AssemblyQualifiedName, flag!.typeName);
            Assert.AreEqual(typeof(bool), flag.defaultValue.ValueType);
        }

        static void RenameVariable(IVariable variable, string newName)
        {
            var titleProp = variable.GetType().GetProperty("Title",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(titleProp, $"Title property not found on {variable.GetType().FullName}.");
            titleProp!.SetValue(variable, newName);
        }

        // Item 10: Rename a variable and re-bake. GUID-based identity means the
        // variable id stays stable across renames — only the display name changes.
        [Test]
        public void ReBake_AfterVariableRename_PreservesVariableId()
        {
            var graph = CreateTestGraph("BakeVarRename");
            var impl = GetImplementation(graph);
            var v = CreateVariable(impl, "health", typeof(int), 100);

            var bake1 = GraphBakerCore.Bake<CardEffectRunner, CardEffectGraphAsset>(
                graph, null, CardEffectGraphRegistry.Instance);
            Assert.IsFalse(bake1.HasErrors);
            Assert.AreEqual(1, bake1.Asset!.variables.Count);
            var id1 = bake1.Asset.variables[0].id;
            Assert.AreEqual("health", bake1.Asset.variables[0].name);

            RenameVariable(v, "hp");
            Assert.AreEqual("hp", v.Name);

            var bake2 = GraphBakerCore.Bake<CardEffectRunner, CardEffectGraphAsset>(
                graph, bake1.Asset, CardEffectGraphRegistry.Instance);
            Assert.IsFalse(bake2.HasErrors);
            Assert.AreEqual(1, bake2.Asset!.variables.Count);

            Assert.AreEqual(id1, bake2.Asset.variables[0].id);
            Assert.AreEqual("hp", bake2.Asset.variables[0].name);
        }
    }
}
