using System.Collections.Generic;

namespace SelfDiagnostic.Models
{
    /// <summary>
    /// RunBook 定义
    /// </summary>
    public sealed class RunbookDefinition
    {
        public string Id { get; set; } = "default";
        public string Title { get; set; } = "默认诊断 RunBook";
        public string Version { get; set; } = "1.0.0";
        public List<RunbookStepDefinition> Steps { get; set; } = new List<RunbookStepDefinition>();
    }

    /// <summary>
    /// RunBook 步骤定义
    /// </summary>
    public sealed class RunbookStepDefinition
    {
        public string StepId { get; set; } = string.Empty;
        public string CheckId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = nameof(CheckCategory.SystemCheck);
        public int TimeoutMs { get; set; } = 5000;
        public bool Enabled { get; set; } = true;
        public string NextOnSuccess { get; set; } = string.Empty;
        public string NextOnFailure { get; set; } = string.Empty;
        public Dictionary<string, string> Params { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// 检查执行结果
    /// </summary>
    public sealed class CheckExecutionOutcome
    {
        public bool Success { get; set; }
    }
}
