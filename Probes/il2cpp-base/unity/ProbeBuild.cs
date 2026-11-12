using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProbeEditor {
    public static class ProbeBuild {
        const string Scene = "Assets/Probe/Probe.unity";

        public static void Build() {
            var backend = Argument("-probeBackend", "il2cpp");
            var stripping = (ManagedStrippingLevel)Enum.Parse(typeof(ManagedStrippingLevel), Argument("-probeStripping", "Minimal"));
            var codegen = Argument("-probeCodegen", "speed");
            var output = Argument("-probeBuild", "Builds/probe");

            MakeScene();
            var target = NamedBuildTarget.Standalone;
            PlayerSettings.SetScriptingBackend(target, backend == "mono" ? ScriptingImplementation.Mono2x : ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(target, stripping);
            PlayerSettings.SetIl2CppCodeGeneration(target, codegen == "size" ? Il2CppCodeGeneration.OptimizeSize : Il2CppCodeGeneration.OptimizeSpeed);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { Scene },
                locationPathName = Path.Combine(output, "Probe.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Debug.Log($"PROBE BUILD {report.summary.result} {report.summary.totalTime} {output}");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static void MakeScene() {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Probe").AddComponent<ProbeRunner.ProbeRunner>();
            EditorSceneManager.SaveScene(scene, Scene);
        }

        static string Argument(string name, string fallback) {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }
    }
}
