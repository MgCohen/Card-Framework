using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Tools.Rulebook
{
    // Reads *.asmdef files under the given roots. References written as "GUID:..." are mapped
    // back to names through the sibling .asmdef.meta files of every asmdef in the project.
    sealed class AsmdefIndex
    {
        public sealed class Entry
        {
            public string Name;
            public string Path;
            public string[] References;
        }

        public IReadOnlyList<Entry> Entries { get; }

        public AsmdefIndex(CheckContext context, IEnumerable<string> roots)
        {
            var guidToName = new Dictionary<string, string>();
            foreach (var dir in new[] { "Assets", "Packages" }.Select(context.Full).Where(Directory.Exists))
            foreach (var file in Directory.EnumerateFiles(dir, "*.asmdef", SearchOption.AllDirectories))
            {
                var meta = file + ".meta";
                if (!File.Exists(meta)) continue;
                var m = Regex.Match(File.ReadAllText(meta), @"guid:\s*([0-9a-f]{32})");
                if (m.Success) guidToName[m.Groups[1].Value] = NameOf(file);
            }

            var entries = new List<Entry>();
            foreach (var root in roots.Select(context.Full).Where(Directory.Exists))
            foreach (var file in Directory.EnumerateFiles(root, "*.asmdef", SearchOption.AllDirectories))
            {
                var json = JObject.Parse(File.ReadAllText(file));
                var refs = (json["references"]?.Values<string>() ?? Enumerable.Empty<string>())
                    .Select(r => r.StartsWith("GUID:") && guidToName.TryGetValue(r.Substring(5), out var n) ? n : r)
                    .ToArray();
                entries.Add(new Entry { Name = json.Value<string>("name"), Path = ProjectPaths.Relative(file), References = refs });
            }
            Entries = entries.OrderBy(e => e.Name).ToList();
        }

        static string NameOf(string file)
        {
            try { return JObject.Parse(File.ReadAllText(file)).Value<string>("name"); }
            catch { return System.IO.Path.GetFileNameWithoutExtension(file); }
        }
    }
}
