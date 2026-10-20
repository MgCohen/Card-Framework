using System.IO;
using UnityEngine;

namespace Tools.Rulebook
{
    public static class ProjectPaths
    {
        public static string Root => Path.GetDirectoryName(Application.dataPath);

        public static string ScriptAssemblies => Path.Combine(Root, "Library", "ScriptAssemblies");

        public static string GateOutput => Path.Combine(Root, "Logs", "gate");

        public static string Relative(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            var full = Path.GetFullPath(path).Replace('\\', '/');
            var root = Root.Replace('\\', '/').TrimEnd('/') + "/";
            return full.StartsWith(root, System.StringComparison.OrdinalIgnoreCase) ? full.Substring(root.Length) : full;
        }
    }
}
