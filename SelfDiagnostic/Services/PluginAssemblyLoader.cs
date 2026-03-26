using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace SelfDiagnostic.Services
{
    /// <summary>
    /// 插件程序集加载器 — 线程安全的按需加载 DLL，支持从 plugins/ 目录、应用程序根目录及 AppDomain 已加载程序集中查找。
    /// </summary>
    public sealed class PluginAssemblyLoader
    {
        private static readonly Lazy<PluginAssemblyLoader> LazyDefault =
            new Lazy<PluginAssemblyLoader>(() => new PluginAssemblyLoader());

        /// <summary>
        /// 进程内单例默认加载器实例。
        /// </summary>
        public static PluginAssemblyLoader Default => LazyDefault.Value;

        private readonly ConcurrentDictionary<string, Assembly> _cache =
            new ConcurrentDictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);

        private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string PluginsDir = Path.Combine(BaseDir, "plugins");

        /// <summary>
        /// 将已加载程序集按短名称加入缓存，避免重复解析（动态程序集会被忽略）。
        /// </summary>
        public void Seed(Assembly assembly)
        {
            if (assembly == null || assembly.IsDynamic) return;
            var name = assembly.GetName().Name;
            if (!string.IsNullOrEmpty(name))
                _cache.TryAdd(name, assembly);
        }

        /// <summary>
        /// 按 DLL 名称或路径加载或从缓存获取程序集；无法解析时返回 null。
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
        /// 从绝对路径加载 DLL 并写入缓存；失败时返回 null（供浏览 DLL 等 UI 使用）。
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

        /// <summary>
        /// 返回当前缓存中已加载程序集的去重列表。
        /// </summary>
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