using System.Threading;
using System.Threading.Tasks;
using SelfDiagnostic.Models;

namespace SelfDiagnostic.Services.Abstractions
{
    public interface ICheckExecutor
    {
        string CheckId { get; }
        Task<CheckExecutionOutcome> ExecuteAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken cancellationToken);
    }
}
