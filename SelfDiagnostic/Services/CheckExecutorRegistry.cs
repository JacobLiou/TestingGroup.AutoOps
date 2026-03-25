using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SelfDiagnostic.Models;
using SelfDiagnostic.Services.Abstractions;

namespace SelfDiagnostic.Services
{
    public sealed class CheckExecutorRegistry
    {
        private readonly Dictionary<string, ICheckExecutor> _executors = new Dictionary<string, ICheckExecutor>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ICheckExecutor> _executorsByMethod = new Dictionary<string, ICheckExecutor>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, CheckExecutorInfo> _infos = new Dictionary<string, CheckExecutorInfo>(StringComparer.OrdinalIgnoreCase);

        public void Register(ICheckExecutor executor, CheckExecutorInfo info)
        {
            _executors[executor.CheckId] = executor;
            _infos[executor.CheckId] = info;
            if (!string.IsNullOrWhiteSpace(info.BindMethod))
            {
                _executorsByMethod[info.BindMethod] = executor;
            }
        }

        public ICheckExecutor Resolve(string checkId)
        {
            _executors.TryGetValue(checkId, out var executor);
            return executor;
        }

        public ICheckExecutor ResolveByMethod(string bindMethod)
        {
            if (string.IsNullOrWhiteSpace(bindMethod)) return null;
            _executorsByMethod.TryGetValue(bindMethod, out var executor);
            return executor;
        }

        public IReadOnlyList<string> GetAllCheckIds()
        {
            return _executors.Keys
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public IReadOnlyList<CheckExecutorInfo> GetAllExecutorInfos()
        {
            return _infos.Values
                .OrderBy(i => i.CheckId, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public CheckExecutorInfo GetExecutorInfo(string checkId)
        {
            _infos.TryGetValue(checkId, out var info);
            return info;
        }
    }

    public sealed class DelegateCheckExecutor : ICheckExecutor
    {
        private readonly Func<DiagnosticItem, RunbookStepDefinition, DiagnosticRunContext, CancellationToken, Task<CheckExecutionOutcome>> _func;

        public DelegateCheckExecutor(
            string checkId,
            Func<DiagnosticItem, RunbookStepDefinition, DiagnosticRunContext, CancellationToken, Task<CheckExecutionOutcome>> func)
        {
            CheckId = checkId;
            _func = func;
        }

        public string CheckId { get; }

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
