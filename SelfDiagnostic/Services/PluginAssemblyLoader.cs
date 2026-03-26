using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SelfDiagnostic.Services
{
    /// <summary>
    /// Thread-safe on-demand assembly loader with caching.
    /// Resolves DLLs from: plugins/ directory, AppDomain base directory, or absolute path.
    /// </summary>
    public sealed class PluginAssemblyLoader
    {
        private static readonly Lazy<PluginAssemblyLoader> LazyDefault =
            new Lazy<PluginAssemblyLoader>(() => new PluginAssemblyLoader());

        public static PluginAssemblyLoader Default => LazyDefault.Value;

        private readonly ConcurrentDictionary<string, Assembly> _cache =
            new ConcurrentDictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string PluginsDir = Path.Combine(BaseDir, "plugins");

        public void Seed(Assembly assembly)
        {
            if (assembly == null || assembly.IsDynamic) return;
            var name = assembly.GetName().Name;
            if (!string.IsNullOrEmpty(name))
                _cache.TryAdd(name, assembly);
        }

        /// <summary>
        /// Load or retrieve a cached assembly by DLL name or path.
        /// Returns null if the assembly cannot be resolved.
        /// </summary>
        public Assembly Load(string bindDll)
        {
            if (string.IsNullOrWhiteSpace(bindDll)) return null;

            var key = NormalizeKey(bindDll);
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var resolved = Resolve(bindDll);
            if (resolved != null)
            {
                _cache.TryAdd(key, resolved);
                var asmName = resolved.GetName().Name;
                if (!string.IsNullOrEmpty(asmName) && !string.Equals(asmName, key, StringComparison.OrdinalIgnoreCase))
                    _cache.TryAdd(asmName, resolved);
            }

            return resolved;
        }

        /// <summary>
        /// Load a DLL from an explicit file path (used by "Browse DLL" UI).
        /// Returns the loaded assembly, or null on failure.
        /// </summary>
        public Assembly LoadFromFile(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath)) return null;

            var assembly = TryLoad(fullPath);
            if (assembly != null)
            {
                var name = assembly.GetName().Name;
                if (!string.IsNullOrEmpty(name))
                    _cache.TryAdd(name, assembly);
            }

            return assembly;
        }

        public IReadOnlyList<Assembly> GetLoadedAssemblies()
        {
            return _cache.Values.Distinct().ToList();
        }

        private static Assembly Resolve(string bindDll)
        {
            if (Path.IsPathRooted(bindDll) && File.Exists(bindDll))
                return TryLoad(bindDll);

            var fileName = EnsureDllExtension(Path.GetFileName(bindDll));

            var pluginPath = Path.Combine(PluginsDir, fileName);
            if (File.Exists(pluginPath))
                return TryLoad(pluginPath);

            var basePath = Path.Combine(BaseDir, fileName);
            if (File.Exists(basePath))
                return TryLoad(basePath);

            var simpleName = Path.GetFileNameWithoutExtension(fileName);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (!asm.IsDynamic && string.Equals(asm.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase))
                        return asm;
                }
                catch { /* skip inaccessible assemblies */ }
            }

            return null;
        }

        private static Assembly TryLoad(string fullPath)
        {
            try { return Assembly.LoadFrom(Path.GetFullPath(fullPath)); }
            catch { return null; }
        }

        private static string NormalizeKey(string bindDll)
        {
            return Path.GetFileNameWithoutExtension(Path.GetFileName(bindDll));
        }

        private static string EnsureDllExtension(string name)
        {
            return name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? name : name + ".dll";
        }
    }
}
