using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SelfDiagnostic.Models;
using SelfDiagnostic.Services.Abstractions;

namespace SelfDiagnostic.Services
{
    public static class DiagnosticEngine
    {
        private static readonly RunbookProvider RunbookProvider = new RunbookProvider();
        private static readonly PluginAssemblyLoader AssemblyLoader = PluginAssemblyLoader.Default;
        private static readonly GenericMethodInvoker GenericInvoker = new GenericMethodInvoker(AssemblyLoader);
        private static readonly CheckExecutorRegistry ExecutorRegistry = BuildExecutorRegistry();
        private static readonly Random Rng = new Random();

        private delegate Task<CheckExecutionOutcome> AsyncCheckExecutorMethod(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct);

        // ──────────────────────────────────────────────────────────────
        //  Public API
        // ──────────────────────────────────────────────────────────────

        public static RunbookDefinition LoadRunbook()
        {
            return RunbookProvider.Load();
        }

        public static IReadOnlyList<string> GetRegisteredCheckIds()
        {
            return ExecutorRegistry.GetAllCheckIds();
        }

        public static IReadOnlyList<CheckExecutorInfo> GetRegisteredExecutorInfos()
        {
            return ExecutorRegistry.GetAllExecutorInfos();
        }

        public static CheckExecutorInfo GetExecutorInfo(string checkId)
        {
            return ExecutorRegistry.GetExecutorInfo(checkId);
        }

        /// <summary>
        /// Browse all callable methods from all loaded non-framework assemblies.
        /// Used by the RunbookEditor to show available binding targets beyond [CheckExecutor] methods.
        /// </summary>
        public static IReadOnlyList<CheckExecutorInfo> BrowseAllLoadedMethods()
        {
            return GenericInvoker.BrowseAllLoadedMethods();
        }

        /// <summary>
        /// Load an external DLL into the assembly cache and return all its callable methods.
        /// Used by the RunbookEditor "Load DLL..." button.
        /// </summary>
        public static IReadOnlyList<CheckExecutorInfo> LoadExternalDll(string dllPath)
        {
            var assembly = AssemblyLoader.LoadFromFile(dllPath);
            if (assembly == null) return new List<CheckExecutorInfo>();
            return GenericMethodInvoker.BrowseAssemblyMethods(assembly);
        }

        public static List<DiagnosticItem> BuildCheckList(RunbookDefinition runbook = null)
        {
            runbook = runbook ?? LoadRunbook();
            return runbook.Steps
                .Where(s => s.Enabled)
                .Select(s => new DiagnosticItem
                {
                    Id = s.CheckId,
                    Name = s.DisplayName,
                    Category = ParseCategory(s.Category)
                })
                .ToList();
        }

        // ──────────────────────────────────────────────────────────────
        //  Core execution — three-tier resolution:
        //    1. Registered executor by BindMethod (fast delegate path)
        //    2. Registered executor by CheckId  (fast delegate path)
        //    3. GenericMethodInvoker via BindDll + BindMethod (reflection)
        // ──────────────────────────────────────────────────────────────

        public static async Task<CheckExecutionOutcome> RunCheckAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            item.Status = CheckStatus.Scanning;
            await Task.Delay(Rng.Next(200, 450), ct);

            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                if (step.TimeoutMs > 0)
                {
                    linkedCts.CancelAfter(step.TimeoutMs);
                }

                try
                {
                    // Tier 1 & 2: Registered executor (backward-compatible fast path)
                    var executor = ExecutorRegistry.ResolveByMethod(step.BindMethod)
                                  ?? ExecutorRegistry.Resolve(step.CheckId);

                    if (executor != null)
                    {
                        return await executor.ExecuteAsync(item, step, runContext, linkedCts.Token);
                    }

                    // Tier 3: Generic reflection invocation via BindDll + BindMethod
                    if (!string.IsNullOrWhiteSpace(step.BindDll) && !string.IsNullOrWhiteSpace(step.BindMethod))
                    {
                        return await GenericInvoker.InvokeAsync(
                            step.BindDll, step.BindMethod, item, step, runContext, linkedCts.Token);
                    }

                    // Unresolvable
                    item.Status = CheckStatus.Warning;
                    item.Detail = string.IsNullOrWhiteSpace(step.BindMethod)
                        ? string.Format("Unregistered check: {0}", step.CheckId)
                        : string.Format("Unbound method: {0} (DLL: {1})", step.BindMethod,
                            string.IsNullOrWhiteSpace(step.BindDll) ? "not specified" : step.BindDll);
                    item.Score = 95;
                    return new CheckExecutionOutcome { Success = false };
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    item.Status = CheckStatus.Fail;
                    item.Detail = string.Format("Check timeout (>{0}ms)", step.TimeoutMs);
                    item.Score = 60;
                    return new CheckExecutionOutcome { Success = false };
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    item.Status = CheckStatus.Warning;
                    item.Detail = string.Format("Check exception: {0}", ex.Message);
                    item.Score = 95;
                    return new CheckExecutionOutcome { Success = false };
                }
            }
        }

        // ──────────────────────────────────────────────────────────────
        //  Registry bootstrap — [CheckExecutor] attribute scanning
        // ──────────────────────────────────────────────────────────────

        private static CheckExecutorRegistry BuildExecutorRegistry()
        {
            var assemblies = DiscoverExecutorAssemblies().ToList();

            foreach (var asm in assemblies)
            {
                AssemblyLoader.Seed(asm);
            }

            var registry = new CheckExecutorRegistry();
            RegisterAttributedExecutors(registry, assemblies);
            return registry;
        }

        private static void RegisterAttributedExecutors(CheckExecutorRegistry registry, IEnumerable<Assembly> assemblies)
        {
            var seenCheckIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var assembly in assemblies)
            {
                foreach (var type in SafeGetTypes(assembly))
                {
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    foreach (var method in methods)
                    {
                        var attribute = method.GetCustomAttribute<CheckExecutorAttribute>();
                        if (attribute == null)
                        {
                            continue;
                        }

                        if (!seenCheckIds.Add(attribute.CheckId))
                        {
                            throw new InvalidOperationException(
                                string.Format("Duplicate check executor registration: {0}, at: {1}.{2}.{3}",
                                    attribute.CheckId, assembly.GetName().Name, type.FullName, method.Name));
                        }

                        var info = new CheckExecutorInfo
                        {
                            CheckId = attribute.CheckId,
                            DisplayName = string.IsNullOrWhiteSpace(attribute.DisplayName) ? attribute.CheckId : attribute.DisplayName,
                            Description = attribute.Description ?? string.Empty,
                            DefaultCategory = string.IsNullOrWhiteSpace(attribute.DefaultCategory) ? "SystemCheck" : attribute.DefaultCategory,
                            MethodName = method.Name,
                            ClassName = type.FullName ?? type.Name,
                            PluginAssembly = assembly.GetName().Name ?? string.Empty
                        };
                        registry.Register(new DelegateCheckExecutor(attribute.CheckId, BuildMethodExecutor(method)), info);
                    }
                }
            }
        }

        private static Func<DiagnosticItem, RunbookStepDefinition, DiagnosticRunContext, CancellationToken, Task<CheckExecutionOutcome>> BuildMethodExecutor(MethodInfo method)
        {
            var parameters = method.GetParameters();

            if (method.ReturnType == typeof(void) &&
                parameters.Length == 1 &&
                parameters[0].ParameterType == typeof(DiagnosticItem))
            {
                var action = (Action<DiagnosticItem>)Delegate.CreateDelegate(typeof(Action<DiagnosticItem>), method);
                return (item, step, ctx, token) =>
                {
                    action(item);
                    return Task.FromResult(new CheckExecutionOutcome
                    {
                        Success = item.Status == CheckStatus.Pass || item.Status == CheckStatus.Fixed
                    });
                };
            }

            if (method.ReturnType == typeof(Task<CheckExecutionOutcome>) &&
                parameters.Length == 4 &&
                parameters[0].ParameterType == typeof(DiagnosticItem) &&
                parameters[1].ParameterType == typeof(RunbookStepDefinition) &&
                parameters[2].ParameterType == typeof(DiagnosticRunContext) &&
                parameters[3].ParameterType == typeof(CancellationToken))
            {
                var action = (AsyncCheckExecutorMethod)Delegate.CreateDelegate(typeof(AsyncCheckExecutorMethod), method);
                return (item, step, runContext, ct) => action(item, step, runContext, ct);
            }

            throw new InvalidOperationException(
                string.Format("检查项执行器方法签名非法: {0}.{1}。支持签名: void (DiagnosticItem) 或 Task<CheckExecutionOutcome> (DiagnosticItem, RunbookStepDefinition, DiagnosticRunContext, CancellationToken)",
                    method.DeclaringType?.FullName, method.Name));
        }

        private static IEnumerable<Assembly> DiscoverExecutorAssemblies()
        {
            var loaded = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
            var current = typeof(DiagnosticEngine).Assembly;
            var currentKey = current.FullName ?? current.GetName().Name ?? "current";
            loaded[currentKey] = current;

            var pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
            if (Directory.Exists(pluginsDir))
            {
                foreach (var dllPath in Directory.EnumerateFiles(pluginsDir, "*.dll", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var assembly = Assembly.LoadFrom(Path.GetFullPath(dllPath));
                        var key = assembly.FullName ?? assembly.GetName().Name ?? dllPath;
                        if (!loaded.ContainsKey(key))
                        {
                            loaded.Add(key, assembly);
                        }
                    }
                    catch
                    {
                        // ignore invalid plugin assemblies
                    }
                }
            }

            return loaded.Values;
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
        }

        private static CheckCategory ParseCategory(string category)
        {
            if (Enum.TryParse<CheckCategory>(category, true, out var parsed))
            {
                return parsed;
            }
            return CheckCategory.SystemCheck;
        }
    }
}
