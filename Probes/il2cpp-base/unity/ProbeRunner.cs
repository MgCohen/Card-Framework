using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ProbeRunner {
    // Runs the Base probe checks in a player and writes one line for each check.
    public sealed class ProbeRunner : MonoBehaviour {
        async void Start() {
            var output = Argument("-probeOutput") ?? Path.Combine(Application.persistentDataPath, "probe.txt");
            var lines = new List<string> { "backend " + Backend() };
            try {
                lines.AddRange(await BaseProbe.ProbeChecks.RunAsync());
            } catch (Exception e) {
                lines.Add("FAIL startup -> " + e);
            }
            int failed = lines.Count(l => l.StartsWith("FAIL", StringComparison.Ordinal));
            lines.Add(failed == 0 ? "RESULT PASS" : "RESULT FAIL " + failed);
            File.WriteAllLines(output, lines);
            Application.Quit(failed == 0 ? 0 : 1);
        }

        static string Backend() {
#if ENABLE_IL2CPP
            return "il2cpp";
#else
            return "mono";
#endif
        }

        static string Argument(string name) {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
