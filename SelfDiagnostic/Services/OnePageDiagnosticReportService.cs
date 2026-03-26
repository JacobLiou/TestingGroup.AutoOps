using Newtonsoft.Json;
using SelfDiagnostic.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SelfDiagnostic.Services
{
    /// <summary>
    /// 一页式诊断报告服务 — 将诊断结果汇总生成结构化的 DiagnosticResultDocument。
    /// </summary>
    public sealed class OnePageDiagnosticReportService
    {
        /// <summary>
        /// 根据诊断项与 Step 映射构建 <see cref="DiagnosticResultDocument"/>，并同时写入 JSON 与 Markdown 报告文件。
        /// </summary>
        public Tuple<string, string, DiagnosticResultDocument> CreateAndSave(
            IReadOnlyCollection<DiagnosticItem> items,
            IReadOnlyDictionary<string, RunbookStepDefinition> stepLookup,
            string stationId, string lineId, string productModel,
            string triggerSource, DateTimeOffset startedAt, DateTimeOffset endedAt)
        {
            var runId = string.Format("{0}_{1}", DateTime.Now.ToString("yyyyMMdd_HHmmss"), stationId);
            var results = items.Select(i => BuildRuleResult(i, stepLookup)).ToList();

            var okCount = results.Count(r => r.ResultLevel == ResultLevel.OK);
            var warnCount = results.Count(r => r.ResultLevel == ResultLevel.Warn);
            var errorCount = results.Count(r => r.ResultLevel == ResultLevel.Error);

            var overallLevel = errorCount > 0 ? ResultLevel.Error : warnCount > 0 ? ResultLevel.Warn : ResultLevel.OK;
            var overallSeverity = errorCount > 0 ? RuleSeverity.S0 : warnCount > 0 ? RuleSeverity.S1 : RuleSeverity.S2;
            var topIssues = results
                .Where(r => r.ResultLevel != ResultLevel.OK)
                .OrderBy(r => r.Severity).ThenBy(r => r.RuleCode)
                .Take(5)
                .Select(r => r.RuleCode + " " + r.RuleName)
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
                Summary = new DiagnosticSummary { OkCount = okCount, WarnCount = warnCount, ErrorCount = errorCount, TopIssues = topIssues },
                Results = results
            };

            var reportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "reports");
            Directory.CreateDirectory(reportDir);

            var jsonPath = Path.Combine(reportDir, "diag_result_" + runId + ".json");
            var mdPath = Path.Combine(reportDir, "diag_report_" + runId + ".md");

            File.WriteAllText(jsonPath, JsonConvert.SerializeObject(document, Formatting.Indented), Encoding.UTF8);
            File.WriteAllText(mdPath, BuildMarkdown(document), Encoding.UTF8);

            return Tuple.Create(jsonPath, mdPath, document);
        }

        private static DiagnosticRuleResult BuildRuleResult(DiagnosticItem item, IReadOnlyDictionary<string, RunbookStepDefinition> stepLookup)
        {
            stepLookup.TryGetValue(item.Id, out var step);
            var metadata = RuleCatalog.Resolve(item.Id);
            var ruleName = step?.DisplayName ?? item.Name;
            ResultLevel level;
            switch (item.Status)
            {
                case CheckStatus.Pass:
                case CheckStatus.Fixed:
                    level = ResultLevel.OK; break;
                case CheckStatus.Warning:
                    level = ResultLevel.Warn; break;
                case CheckStatus.Fail:
                    level = ResultLevel.Error; break;
                default:
                    level = ResultLevel.Warn; break;
            }

            var failReason = level == ResultLevel.OK ? "N/A" : metadata.DefaultFailReason;
            var action = level == ResultLevel.OK ? "N/A"
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
            sb.AppendLine("- RunId: `" + document.RunId + "`");
            sb.AppendLine(string.Format("- 工位: `{0}`  线体: `{1}`  型号: `{2}`", document.StationId, document.LineId, document.ProductModel));
            sb.AppendLine("- 触发方式: `" + document.TriggerSource + "`");
            sb.AppendLine(string.Format("- 时间: `{0}` ~ `{1}`", document.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"), document.EndedAt.ToString("yyyy-MM-dd HH:mm:ss")));
            sb.AppendLine(string.Format("- 总体结论: `{0}` / 严重级 `{1}` / 允许运行: `{2}`", document.OverallLevel, document.OverallSeverity, document.AllowProduction));
            sb.AppendLine();
            sb.AppendLine("## 摘要");
            sb.AppendLine("- OK: `" + document.Summary.OkCount + "`");
            sb.AppendLine("- Warn: `" + document.Summary.WarnCount + "`");
            sb.AppendLine("- Error: `" + document.Summary.ErrorCount + "`");
            sb.AppendLine();
            sb.AppendLine("## Top问题");
            if (document.Summary.TopIssues.Count == 0) { sb.AppendLine("- 无"); }
            else { foreach (var issue in document.Summary.TopIssues) { sb.AppendLine("- " + issue); } }
            sb.AppendLine();
            sb.AppendLine("## 全量结果");
            sb.AppendLine("| RuleCode | CheckId | Name | Level | Severity | Threshold | Detail |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var item in document.Results)
            {
                sb.AppendLine(string.Format("| {0} | {1} | {2} | {3} | {4} | {5} | {6} |",
                    item.RuleCode, item.CheckId, SanitizeCell(item.RuleName), item.ResultLevel,
                    item.Severity, SanitizeCell(item.Threshold), SanitizeCell(item.Detail)));
            }
            return sb.ToString();
        }

        private static string SanitizeCell(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            return text.Replace("|", "\\|").Replace("\r", " ").Replace("\n", "<br/>");
        }
    }
}