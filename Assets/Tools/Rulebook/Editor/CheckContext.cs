using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;

namespace Tools.Rulebook
{
    // Shared views of the project for one check run: loaded assemblies by name, Cecil modules
    // read from Library/ScriptAssemblies, and paths relative to the project root.
    public sealed class CheckContext : IDisposable
    {
        public string Root { get; }

        readonly Dictionary<string, Assembly> _loaded = new();
        readonly Dictionary<string, ModuleDefinition> _modules = new();
        DefaultAssemblyResolver _resolver;

        public CheckContext(string root = null)
        {
            Root = root ?? ProjectPaths.Root;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (a.IsDynamic) continue;
                var name = a.GetName().Name;
                if (!_loaded.ContainsKey(name)) _loaded[name] = a;
            }
        }

        public string Full(string relative) => Path.GetFullPath(Path.Combine(Root, relative));

        public Assembly Loaded(string name) => _loaded.TryGetValue(name, out var a) ? a : null;

        public IEnumerable<Assembly> Select(IEnumerable<string> include, IEnumerable<string> exclude)
            => _loaded.Where(kv => Glob.Any(include, kv.Key) && !Glob.Any(exclude, kv.Key))
                      .OrderBy(kv => kv.Key)
                      .Select(kv => kv.Value);

        public static Type[] TypesOf(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null).ToArray(); }
        }

        public ModuleDefinition Module(string assemblyName)
        {
            if (_modules.TryGetValue(assemblyName, out var cached)) return cached;

            var path = Path.Combine(ProjectPaths.ScriptAssemblies, assemblyName + ".dll");
            if (!File.Exists(path))
            {
                var loaded = Loaded(assemblyName);
                path = loaded != null && !string.IsNullOrEmpty(loaded.Location) ? loaded.Location : null;
            }
            if (path == null) return _modules[assemblyName] = null;

            var parameters = new ReaderParameters { AssemblyResolver = Resolver(), InMemory = true, ReadSymbols = true };
            ModuleDefinition module;
            try { module = ModuleDefinition.ReadModule(path, parameters); }
            catch
            {
                parameters.ReadSymbols = false;
                module = ModuleDefinition.ReadModule(path, parameters);
            }
            return _modules[assemblyName] = module;
        }

        DefaultAssemblyResolver Resolver()
        {
            if (_resolver != null) return _resolver;
            _resolver = new DefaultAssemblyResolver();
            var dirs = new HashSet<string> { ProjectPaths.ScriptAssemblies };
            foreach (var a in _loaded.Values)
                if (!string.IsNullOrEmpty(a.Location)) dirs.Add(Path.GetDirectoryName(a.Location));
            foreach (var d in dirs.Where(Directory.Exists)) _resolver.AddSearchDirectory(d);
            return _resolver;
        }

        public void Dispose()
        {
            foreach (var m in _modules.Values) m?.Dispose();
            _modules.Clear();
            _resolver?.Dispose();
        }
    }
}
