using System.Threading;
using System.Threading.Tasks;
using SelfDiagnostic.Models;

namespace SelfDiagnostic.Services.Abstractions
{
    /// <summary>
    /// 诊断检查执行器接口（主项目副本）。
    /// </summary>
    public interface ICheckExecutor
    {
        /// <summary>
        /// 检查项 ID。
        /// </summary>
        string CheckId { get; }
        /// <summary>
        /// 异步执行诊断检查并返回执行结果。
        /// </summary>
        Task<CheckExecutionOutcome> ExecuteAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken cancellationToken);
    }
}
