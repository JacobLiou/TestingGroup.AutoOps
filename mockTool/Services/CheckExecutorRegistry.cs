using MockDiagTool.Models;
using MockDiagTool.Services.Abstractions;

namespace MockDiagTool.Services;

public sealed class CheckExecutorRegistry
{
    private readonly Dictionary<string, ICheckExecutor> _executors = new(StringComparer.OrdinalIgnoreCase);

    public void Register(ICheckExecutor executor)
    {
        _executors[executor.CheckId] = executor;
    }

    public ICheckExecutor? Resolve(string checkId)
    {
        _executors.TryGetValue(checkId, out var executor);
        return executor;
    }
}

public sealed class DelegateCheckExecutor : ICheckExecutor
{
    private readonly Func<DiagnosticItem, RunbookStepDefinition, DiagnosticRunContext?, CancellationToken, Task<CheckExecutionOutcome>> _func;

    public DelegateCheckExecutor(
        string checkId,
        Func<DiagnosticItem, RunbookStepDefinition, DiagnosticRunContext?, CancellationToken, Task<CheckExecutionOutcome>> func)
    {
        CheckId = checkId;
        _func = func;
    }

    public string CheckId { get; }

    public Task<CheckExecutionOutcome> ExecuteAsync(
        DiagnosticItem item,
        RunbookStepDefinition step,
        DiagnosticRunContext? runContext,
        CancellationToken cancellationToken)
    {
        return _func(item, step, runContext, cancellationToken);
    }
}
