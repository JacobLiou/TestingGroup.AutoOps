using System;
using System.Collections.Generic;

namespace SelfDiagnostic.Models
{
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
        public int OkCount { get; set; }
        public int WarnCount { get; set; }
        public int ErrorCount { get; set; }
        public List<string> TopIssues { get; set; } = new List<string>();
    }

    public sealed class DiagnosticRuleResult
    {
        public string RuleCode { get; set; } = string.Empty;
        public string RuleName { get; set; } = string.Empty;
        public string CheckId { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public ResultLevel ResultLevel { get; set; }
        public RuleSeverity Severity { get; set; }
        public string Threshold { get; set; } = string.Empty;
        public string FailReason { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Escalation { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public int Score { get; set; }
    }

    public sealed class DiagnosticResultDocument
    {
        public string SchemaVersion { get; set; } = "1.0.0";
        public string RunId { get; set; } = string.Empty;
        public string StationId { get; set; } = string.Empty;
        public string LineId { get; set; } = string.Empty;
        public string ProductModel { get; set; } = string.Empty;
        public string TriggerSource { get; set; } = string.Empty;
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset EndedAt { get; set; }
        public ResultLevel OverallLevel { get; set; }
        public RuleSeverity OverallSeverity { get; set; }
        public bool AllowProduction { get; set; }
        public DiagnosticSummary Summary { get; set; } = new DiagnosticSummary();
        public List<DiagnosticRuleResult> Results { get; set; } = new List<DiagnosticRuleResult>();
    }

    public sealed class RuleMetadata
    {
        public string CheckId { get; set; } = string.Empty;
        public string RuleCode { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public RuleSeverity Severity { get; set; } = RuleSeverity.S1;
        public string Threshold { get; set; } = string.Empty;
        public string DefaultFailReason { get; set; } = string.Empty;
        public string DefaultAction { get; set; } = string.Empty;
        public string EscalationPath { get; set; } = string.Empty;
    }
}
