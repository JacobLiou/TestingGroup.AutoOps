using System.Text;
using System.Text.Json;
using System.IO;
using MockDiagTool.Models;

namespace MockDiagTool.Services;

public sealed class OnePageDiagnosticReportService
{
    public (string JsonPath, string MarkdownPath, DiagnosticResultDocument Document) CreateAndSave(
        IReadOnlyCollection<DiagnosticItem> items,
        IReadOnlyDictionary<string, RunbookStepDefinition> stepLookup,
        string stationId,
        string lineId,
        string productModel,
        string triggerSource,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt)
    {
        var runId = $"{DateTime.Now:yyyyMMdd_HHmmss}_{stationId}";
        var results = items.Select(i => BuildRuleResult(i, stepLookup)).ToList();

        var okCount = results.Count(r => r.ResultLevel == ResultLevel.OK);
        var warnCount = results.Count(r => r.ResultLevel == ResultLevel.Warn);
        var errorCount = results.Count(r => r.ResultLevel == ResultLevel.Error);

        var overallLevel = errorCount > 0 ? ResultLevel.Error : warnCount > 0 ? ResultLevel.Warn : ResultLevel.OK;
        var overallSeverity = errorCount > 0 ? RuleSeverity.S0 : warnCount > 0 ? RuleSeverity.S1 : RuleSeverity.S2;
        var topIssues = results
            .Where(r => r.ResultLevel != ResultLevel.OK)
            .OrderBy(r => r.Severity)
            .ThenBy(r => r.RuleCode)
            .Take(5)
            .Select(r => $"{r.RuleCode} {r.RuleName}")
            .ToList();

        var document = new DiagnosticResultDocument
        {
            RunId = runId,
            StationId = stationId,
            LineId = lineId,
            ProductModel = productModel,
            TriggerSource = triggerSource,
            StartedAt = startedAt,
            EndedAt = endedAt,
            OverallLevel = overallLevel,
            OverallSeverity = overallSeverity,
            AllowProduction = errorCount == 0,
            Summary = new DiagnosticSummary
            {
                OkCount = okCount,
                WarnCount = warnCount,
                ErrorCount = errorCount,
                TopIssues = topIssues
            },
            Results = results
        };

        var reportDir = Path.Combine(AppContext.BaseDirectory, "logs", "reports");
        Directory.CreateDirectory(reportDir);

        var jsonPath = Path.Combine(reportDir, $"diag_result_{runId}.json");
        var mdPath = Path.Combine(reportDir, $"diag_report_{runId}.md");

        File.WriteAllText(jsonPath, JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
        File.WriteAllText(mdPath, BuildMarkdown(document), Encoding.UTF8);

        return (jsonPath, mdPath, document);
    }

    private static DiagnosticRuleResult BuildRuleResult(
        DiagnosticItem item,
        IReadOnlyDictionary<string, RunbookStepDefinition> stepLookup)
    {
        stepLookup.TryGetValue(item.Id, out var step);
        var metadata = RuleCatalog.Resolve(item.Id);
        var ruleName = step?.DisplayName ?? item.Name;
        var level = item.Status switch
        {
            CheckStatus.Pass or CheckStatus.Fixed => ResultLevel.OK,
            CheckStatus.Warning => ResultLevel.Warn,
            CheckStatus.Fail => ResultLevel.Error,
            _ => ResultLevel.Warn
        };

        var failReason = level == ResultLevel.OK ? "N/A" : metadata.DefaultFailReason;
        var action = level == ResultLevel.OK
            ? "N/A"
            : string.IsNullOrWhiteSpace(item.FixSuggestion) ? metadata.DefaultAction : item.FixSuggestion;
        var escalation = level == ResultLevel.OK ? "N/A" : metadata.EscalationPath;

        return new DiagnosticRuleResult
        {
            RuleCode = metadata.RuleCode,
            RuleName = ruleName,
            CheckId = item.Id,
            Domain = metadata.Domain,
            Category = metadata.Category,
            ResultLevel = level,
            Severity = metadata.Severity,
            Threshold = metadata.Threshold,
            FailReason = failReason,
            Action = action,
            Escalation = escalation,
            Detail = item.Detail,
            Score = item.Score
        };
    }

    private static string BuildMarkdown(DiagnosticResultDocument document)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# 自动诊断一页报告");
        sb.AppendLine();
        sb.AppendLine($"- RunId: `{document.RunId}`");
        sb.AppendLine($"- 工位: `{document.StationId}`  线体: `{document.LineId}`  型号: `{document.ProductModel}`");
        sb.AppendLine($"- 触发方式: `{document.TriggerSource}`");
        sb.AppendLine($"- 时间: `{document.StartedAt:yyyy-MM-dd HH:mm:ss}` ~ `{document.EndedAt:yyyy-MM-dd HH:mm:ss}`");
        sb.AppendLine($"- 总体结论: `{document.OverallLevel}` / 严重级 `{document.OverallSeverity}` / 允许运行: `{document.AllowProduction}`");
        sb.AppendLine();
        sb.AppendLine("## 摘要");
        sb.AppendLine($"- OK: `{document.Summary.OkCount}`");
        sb.AppendLine($"- Warn: `{document.Summary.WarnCount}`");
        sb.AppendLine($"- Error: `{document.Summary.ErrorCount}`");
        sb.AppendLine();
        sb.AppendLine("## Top问题");
        if (document.Summary.TopIssues.Count == 0)
        {
            sb.AppendLine("- 无");
        }
        else
        {
            foreach (var issue in document.Summary.TopIssues)
            {
                sb.AppendLine($"- {issue}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## 异常/风险处置");
        var abnormal = document.Results.Where(r => r.ResultLevel != ResultLevel.OK).OrderBy(r => r.Severity).ToList();
        if (abnormal.Count == 0)
        {
            sb.AppendLine("- 无异常项");
        }
        else
        {
            foreach (var item in abnormal)
            {
                sb.AppendLine($"- `{item.RuleCode}` {item.RuleName} | {item.ResultLevel} | 原因: {item.FailReason} | 动作: {item.Action} | 升级: {item.Escalation}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## 全量结果");
        sb.AppendLine("| RuleCode | CheckId | Name | Level | Severity | Threshold | Detail |");
        sb.AppendLine("|---|---|---|---|---|---|---|");
        foreach (var item in document.Results)
        {
            sb.AppendLine($"| {item.RuleCode} | {item.CheckId} | {SanitizeCell(item.RuleName)} | {item.ResultLevel} | {item.Severity} | {SanitizeCell(item.Threshold)} | {SanitizeCell(item.Detail)} |");
        }

        return sb.ToString();
    }

    private static string SanitizeCell(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text.Replace("|", "\\|").Replace("\r", " ").Replace("\n", "<br/>");
    }
}
