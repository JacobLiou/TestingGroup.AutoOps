using System.Collections.Generic;

namespace SelfDiagnostic.Models
{
    public sealed class RunbookDefinition
    {
        public string Id { get; set; } = "default";
        public string Title { get; set; } = "默认诊断 RunBook";
        public string Version { get; set; } = "1.0.0";
        public List<RunbookStepDefinition> Steps { get; set; } = new List<RunbookStepDefinition>();
    }

    public sealed class RunbookStepDefinition
    {
        public string CheckId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = nameof(CheckCategory.SystemCheck);
        public string BindDll { get; set; } = string.Empty;
        public string BindMethod { get; set; } = string.Empty;
        public int TimeoutMs { get; set; } = 5000;
        public bool Enabled { get; set; } = true;
        public Dictionary<string, string> Params { get; set; } = new Dictionary<string, string>();
    }

    public sealed class CheckExecutionOutcome
    {
        public bool Success { get; set; }
    }
}
