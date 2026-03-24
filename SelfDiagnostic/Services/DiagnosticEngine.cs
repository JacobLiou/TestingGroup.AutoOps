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
        private static readonly CheckExecutorRegistry ExecutorRegistry = BuildExecutorRegistry();
        private static readonly Random Rng = new Random();

        private delegate Task<CheckExecutionOutcome> AsyncCheckExecutorMethod(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct);

        public static RunbookDefinition LoadRunbook()
        {
            return RunbookProvider.Load();
        }

        public static IReadOnlyList<string> GetRegisteredCheckIds()
        {
            return ExecutorRegistry.GetAllCheckIds();
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

        public static async Task<CheckExecutionOutcome> RunCheckAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken ct)
        {
            item.Status = CheckStatus.Scanning;
            await Task.Delay(Rng.Next(200, 450), ct);

            var executor = ExecutorRegistry.Resolve(step.CheckId);
            if (executor == null)
            {
                item.Status = CheckStatus.Warning;
                item.Detail = LocF("Loc.Diag.UnregisteredCheck", "未注册的检查项: {0}", step.CheckId);
                item.Score = 95;
                return new CheckExecutionOutcome { Success = false };
            }

            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                if (step.TimeoutMs > 0)
                {
                    linkedCts.CancelAfter(step.TimeoutMs);
                }

                try
                {
                    return await executor.ExecuteAsync(item, step, runContext, linkedCts.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    item.Status = CheckStatus.Fail;
                    item.Detail = LocF("Loc.Diag.Timeout", "检查超时（>{0}ms）", step.TimeoutMs);
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
                    item.Detail = LocF("Loc.Diag.Exception", "检查时发生异常: {0}", ex.Message);
                    item.Score = 95;
                    return new CheckExecutionOutcome { Success = false };
                }
            }
        }

        private static CheckExecutorRegistry BuildExecutorRegistry()
        {
            var registry = new CheckExecutorRegistry();
            RegisterAttributedExecutors(registry, DiscoverExecutorAssemblies());
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
                                string.Format("重复的检查项执行器注册: {0}，位置: {1}.{2}.{3}",
                                    attribute.CheckId, assembly.GetName().Name, type.FullName, method.Name));
                        }

                        registry.Register(new DelegateCheckExecutor(attribute.CheckId, BuildMethodExecutor(method)));
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

        private static string LocF(string key, string fallbackFormat, params object[] args)
        {
            return LanguageService.Instance.Format(key, fallbackFormat, args);
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
