using SelfDiagnostic.Models;
using SelfDiagnostic.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SelfDiagnostic.Services
{
    /// <summary>
    /// 检查执行器注册表 — 在启动时通过反射扫描所有插件 DLL，发现并注册标注了 [CheckExecutor] 特性的方法。
    /// </summary>
    public sealed class CheckExecutorRegistry
    {
        private readonly Dictionary<string, ICheckExecutor> _executors = new Dictionary<string, ICheckExecutor>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ICheckExecutor> _executorsByMethod = new Dictionary<string, ICheckExecutor>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CheckExecutorInfo> _infos = new Dictionary<string, CheckExecutorInfo>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 注册检查执行器及其元数据（可按 CheckId 与 BindMethod 解析）。
        /// </summary>
        public void Register(ICheckExecutor executor, CheckExecutorInfo info)
        {
            _executors[executor.CheckId] = executor;
            _infos[executor.CheckId] = info;
            if (!string.IsNullOrWhiteSpace(info.BindMethod))
            {
                _executorsByMethod[info.BindMethod] = executor;
            }
        }

        /// <summary>
        /// 根据 CheckId 解析已注册的执行器；未找到时返回 null。
        /// </summary>
        public ICheckExecutor Resolve(string checkId)
        {
            _executors.TryGetValue(checkId, out var executor);
            return executor;
        }

        /// <summary>
        /// 根据 BindMethod 解析已注册的执行器；空字符串时返回 null。
        /// </summary>
        public ICheckExecutor ResolveByMethod(string bindMethod)
        {
            if (string.IsNullOrWhiteSpace(bindMethod)) return null;
            _executorsByMethod.TryGetValue(bindMethod, out var executor);
            return executor;
        }

        /// <summary>
        /// 获取所有已注册的 CheckId 列表（忽略大小写排序）。
        /// </summary>
        public IReadOnlyList<string> GetAllCheckIds()
        {
            return _executors.Keys
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// 获取所有执行器的元信息列表（按 CheckId 排序）。
        /// </summary>
        public IReadOnlyList<CheckExecutorInfo> GetAllExecutorInfos()
        {
            return _infos.Values
                .OrderBy(i => i.CheckId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// 根据 CheckId 获取执行器元信息；未找到时返回 null。
        /// </summary>
        public CheckExecutorInfo GetExecutorInfo(string checkId)
        {
            _infos.TryGetValue(checkId, out var info);
            return info;
        }
    }

    /// <summary>
    /// 委托型检查执行器 — 将异步委托封装为 <see cref="ICheckExecutor"/>，供注册表统一调度。
    /// </summary>
    public sealed class DelegateCheckExecutor : ICheckExecutor
    {
        private readonly Func<DiagnosticItem, RunbookStepDefinition, DiagnosticRunContext, CancellationToken, Task<CheckExecutionOutcome>> _func;

        /// <summary>
        /// 使用指定的 CheckId 与执行委托创建实例。
        /// </summary>
        public DelegateCheckExecutor(
            string checkId,
            Func<DiagnosticItem, RunbookStepDefinition, DiagnosticRunContext, CancellationToken, Task<CheckExecutionOutcome>> func)
        {
            CheckId = checkId;
            _func = func;
        }

        /// <summary>
        /// 检查项标识（与 RunBook 中 CheckId 对应）。
        /// </summary>
        public string CheckId { get; }

        /// <summary>
        /// 异步执行检查：调用内部委托并返回 <see cref="CheckExecutionOutcome"/>。
        /// </summary>
        public Task<CheckExecutionOutcome> ExecuteAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken cancellationToken)
        {
            return _func(item, step, runContext, cancellationToken);
        }
    }
}