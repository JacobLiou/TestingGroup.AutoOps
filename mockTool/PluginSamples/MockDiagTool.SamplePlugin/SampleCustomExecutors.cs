using MockDiagTool.Models;
using MockDiagTool.Services.Abstractions;

namespace MockDiagTool.SamplePlugin;

public static class SampleCustomExecutors
{
    // 示例插件检查项：在 runbook 里使用 checkId = "PLG_001" 即可调用
    [CheckExecutor("PLG_001")]
    private static void CheckSamplePlugin(DiagnosticItem item)
    {
        item.Status = CheckStatus.Pass;
        item.Score = 100;
        item.Detail = "插件检查项执行成功（MockDiagTool.SamplePlugin）";
        item.FixSuggestion = string.Empty;
    }

    // 示例异步签名：可读取 step.Params / runContext 并返回显式 outcome
    [CheckExecutor("PLG_002")]
    private static Task<CheckExecutionOutcome> CheckSampleAsync(
        DiagnosticItem item,
        RunbookStepDefinition step,
        DiagnosticRunContext? runContext,
        CancellationToken ct)
    {
        var note = step.Params.TryGetValue("note", out var v) && !string.IsNullOrWhiteSpace(v)
            ? v
            : "no-note";
        item.Status = CheckStatus.Pass;
        item.Score = 100;
        item.Detail = $"异步插件检查执行成功，note={note}";
        item.FixSuggestion = string.Empty;
        return Task.FromResult(new CheckExecutionOutcome { Success = true });
    }
}
