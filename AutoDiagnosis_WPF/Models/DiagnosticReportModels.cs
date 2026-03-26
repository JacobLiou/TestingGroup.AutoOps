namespace MockDiagTool.Models;

public enum ResultLevel
{
    OK,
    Warn,
    Error
}

public enum RuleSeverity
{
    S0,
    S1,
    S2
}

public sealed class DiagnosticSummary
{
    public int OkCount { get; init; }
    public int WarnCount { get; init; }
    public int ErrorCount { get; init; }
    public List<string> TopIssues { get; init; } = [];
}

public sealed class DiagnosticRuleResult
{
    public string RuleCode { get; init; } = string.Empty;
    public string RuleName { get; init; } = string.Empty;
    public string CheckId { get; init; } = string.Empty;
    public string Domain { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public ResultLevel ResultLevel { get; init; }
    public RuleSeverity Severity { get; init; }
    public string Threshold { get; init; } = string.Empty;
    public string FailReason { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Escalation { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public int Score { get; init; }
}

public sealed class DiagnosticResultDocument
{
    public string SchemaVersion { get; init; } = "1.0.0";
    public string RunId { get; init; } = string.Empty;
    public string StationId { get; init; } = string.Empty;
    public string LineId { get; init; } = string.Empty;
    public string ProductModel { get; init; } = string.Empty;
    public string TriggerSource { get; init; } = string.Empty;
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset EndedAt { get; init; }
    public ResultLevel OverallLevel { get; init; }
    public RuleSeverity OverallSeverity { get; init; }
    public bool AllowProduction { get; init; }
    public DiagnosticSummary Summary { get; init; } = new();
    public List<DiagnosticRuleResult> Results { get; init; } = [];
}

public sealed class RuleMetadata
{
    public string CheckId { get; init; } = string.Empty;
    public string RuleCode { get; init; } = string.Empty;
    public string Domain { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public RuleSeverity Severity { get; init; } = RuleSeverity.S1;
    public string Threshold { get; init; } = string.Empty;
    public string DefaultFailReason { get; init; } = string.Empty;
    public string DefaultAction { get; init; } = string.Empty;
    public string EscalationPath { get; init; } = string.Empty;
}
