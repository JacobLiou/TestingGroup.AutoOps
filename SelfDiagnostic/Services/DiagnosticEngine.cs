using SelfDiagnostic.Models;
using SelfDiagnostic.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace SelfDiagnostic.Services
{
    /// <summary>
    /// 诊断引擎 — 核心调度器，负责加载 RunBook、构建检查列表、按三级策略（注册的执行器 → CheckId 查找 → GenericMethodInvoker 通用调用）执行每个 Step。
    /// </summary>
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

        /// <summary>
        /// 通过默认 <see cref="RunbookProvider"/> 加载 RunBook 定义。
        /// </summary>
        public static RunbookDefinition LoadRunbook()
        {
            return RunbookProvider.Load();
        }

        /// <summary>
        /// 获取启动时已注册的全部 CheckId。
        /// </summary>
        public static IReadOnlyList<string> GetRegisteredCheckIds()
        {
            return ExecutorRegistry.GetAllCheckIds();
        }

        /// <summary>
        /// 获取启动时已注册的全部执行器元信息。
        /// </summary>
        public static IReadOnlyList<CheckExecutorInfo> GetRegisteredExecutorInfos()
        {
            return ExecutorRegistry.GetAllExecutorInfos();
        }

        /// <summary>
        /// 根据 CheckId 查询已注册执行器的元信息。
        /// </summary>
        public static CheckExecutorInfo GetExecutorInfo(string checkId)
        {
            return ExecutorRegistry.GetExecutorInfo(checkId);
        }

        /// <summary>
        /// 浏览当前已加载的非框架程序集中所有可调用方法；供 RunBook 编辑器展示除 [CheckExecutor] 外的绑定目标。
        /// </summary>
        public static IReadOnlyList<CheckExecutorInfo> BrowseAllLoadedMethods()
        {
            return GenericInvoker.BrowseAllLoadedMethods();
        }

        /// <summary>
        /// 将外部 DLL 载入程序集缓存并返回其中可调用方法列表；供 RunBook 编辑器「加载 DLL」使用。
        /// </summary>
        public static IReadOnlyList<CheckExecutorInfo> LoadExternalDll(string dllPath)
        {
            var assembly = AssemblyLoader.LoadFromFile(dllPath);
            if (assembly == null) return new List<CheckExecutorInfo>();
            return GenericMethodInvoker.BrowseAssemblyMethods(assembly);
        }

        /// <summary>
        /// 根据 RunBook 中已启用的 Step 构建诊断项列表；runbook 为 null 时加载默认 RunBook。
        /// </summary>
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

        /// <summary>
        /// 异步执行单个检查：先尝试注册执行器（BindMethod / CheckId），再回退到 GenericMethodInvoker（BindDll + BindMethod）。
        /// </summary>
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
                    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
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

            if (method.IsStatic &&
                method.ReturnType == typeof(void) &&
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

            if (method.IsStatic &&
                method.ReturnType == typeof(Task<CheckExecutionOutcome>) &&
                parameters.Length == 4 &&
                parameters[0].ParameterType == typeof(DiagnosticItem) &&
                parameters[1].ParameterType == typeof(RunbookStepDefinition) &&
                parameters[2].ParameterType == typeof(DiagnosticRunContext) &&
                parameters[3].ParameterType == typeof(CancellationToken))
            {
                var action = (AsyncCheckExecutorMethod)Delegate.CreateDelegate(typeof(AsyncCheckExecutorMethod), method);
                return (item, step, runContext, ct) => action(item, step, runContext, ct);
            }

            object instance = null;
            if (!method.IsStatic)
            {
                instance = GenericMethodInvoker.TryCreateInstance(method.DeclaringType);
                if (instance == null)
                {
                    throw new InvalidOperationException(
                        string.Format("Cannot create instance of {0} (no parameterless constructor) for [CheckExecutor] method {1}",
                            method.DeclaringType?.FullName, method.Name));
                }
            }

            return async (item, step, runContext, ct) =>
            {
                var args = GenericMethodInvoker.MapParameters(method, item, step, runContext, ct);
                try
                {
                    var rawResult = method.Invoke(instance, args);
                    return await GenericMethodInvoker.AdaptReturnValue(rawResult, item);
                }
                catch (TargetInvocationException tex)
                {
                    var inner = tex.InnerException ?? tex;
                    if (inner is OperationCanceledException && ct.IsCancellationRequested)
                        throw inner;

                    item.Status = CheckStatus.Fail;
                    item.Detail = string.Format("{0}.{1} threw: {2}",
                        method.DeclaringType?.FullName, method.Name, inner.Message);
                    return new CheckExecutionOutcome { Success = false };
                }
            };
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