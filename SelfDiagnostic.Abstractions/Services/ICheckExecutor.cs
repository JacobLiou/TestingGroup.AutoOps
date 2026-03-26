using SelfDiagnostic.Models;
using System.Threading;
using System.Threading.Tasks;

namespace SelfDiagnostic.Services.Abstractions
{
    /// <summary>
    /// 诊断检查执行器接口 — 每个 [CheckExecutor] 标注的方法在内部会被包装为此接口的实例。
    /// </summary>
    public interface ICheckExecutor
    {
        /// <summary>关联的检查项 ID</summary>
        string CheckId { get; }

        /// <summary>
        /// 异步执行检查逻辑，将结果写入 <paramref name="item"/>，并返回执行结果。
        /// </summary>
        Task<CheckExecutionOutcome> ExecuteAsync(
            DiagnosticItem item,
            RunbookStepDefinition step,
            DiagnosticRunContext runContext,
            CancellationToken cancellationToken);
    }
}