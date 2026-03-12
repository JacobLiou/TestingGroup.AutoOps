using MockDiagTool.Models;

namespace MockDiagTool.Services.Abstractions;

public interface ICheckExecutor
{
    string CheckId { get; }
    Task<CheckExecutionOutcome> ExecuteAsync(
        DiagnosticItem item,
        RunbookStepDefinition step,
        DiagnosticRunContext? runContext,
        CancellationToken cancellationToken);
}
