using System;
using System.Collections.Generic;

namespace SelfDiagnostic.Models
{
    /// <summary>
    /// 诊断结果等级
    /// </summary>
    public enum ResultLevel
    {
        /// <summary>正常</summary>
        OK,
        /// <summary>警告</summary>
        Warn,
        /// <summary>错误</summary>
        Error
    }

    /// <summary>
    /// 规则严重级别（S0 最高，S2 最低）
    /// </summary>
    public enum RuleSeverity
    {
        /// <summary>最高严重级别 — 必须立即处理</summary>
        S0,
        /// <summary>中等严重级别 — 需要关注</summary>
        S1,
        /// <summary>低严重级别 — 建议修复</summary>
        S2
    }

    /// <summary>
    /// 诊断结果摘要（汇总 OK / Warn / Error 数量及主要问题）
    /// </summary>
    public sealed class DiagnosticSummary
    {
        public int OkCount { get; set; }
        public int WarnCount { get; set; }
        public int ErrorCount { get; set; }

        /// <summary>最突出的问题描述列表（用于报告首页展示）</summary>
        public List<string> TopIssues { get; set; } = new List<string>();
    }

    /// <summary>
    /// 单条诊断规则的执行结果
    /// </summary>
    public sealed class DiagnosticRuleResult
    {
        /// <summary>规则代码（如 R001）</summary>
        public string RuleCode { get; set; } = string.Empty;
        /// <summary>规则名称</summary>
        public string RuleName { get; set; } = string.Empty;
        /// <summary>关联的检查项 ID</summary>
        public string CheckId { get; set; } = string.Empty;
        /// <summary>所属领域</summary>
        public string Domain { get; set; } = string.Empty;
        /// <summary>所属分类</summary>
        public string Category { get; set; } = string.Empty;
        /// <summary>结果等级</summary>
        public ResultLevel ResultLevel { get; set; }
        /// <summary>严重级别</summary>
        public RuleSeverity Severity { get; set; }
        /// <summary>判定阈值说明</summary>
        public string Threshold { get; set; } = string.Empty;
        /// <summary>失败原因</summary>
        public string FailReason { get; set; } = string.Empty;
        /// <summary>建议操作</summary>
        public string Action { get; set; } = string.Empty;
        /// <summary>升级路径（需要进一步排查时的联系方式/流程）</summary>
        public string Escalation { get; set; } = string.Empty;
        /// <summary>详细描述</summary>
        public string Detail { get; set; } = string.Empty;
        /// <summary>得分</summary>
        public int Score { get; set; }
    }

    /// <summary>
    /// 一次完整诊断运行的结果文档（可序列化为 JSON 报告）
    /// </summary>
    public sealed class DiagnosticResultDocument
    {
        public string SchemaVersion { get; set; } = "1.0.0";
        /// <summary>本次运行唯一 ID</summary>
        public string RunId { get; set; } = string.Empty;
        /// <summary>工站 ID</summary>
        public string StationId { get; set; } = string.Empty;
        /// <summary>产线 ID</summary>
        public string LineId { get; set; } = string.Empty;
        /// <summary>产品型号</summary>
        public string ProductModel { get; set; } = string.Empty;
        /// <summary>触发来源（手动 / 自动 / 定时）</summary>
        public string TriggerSource { get; set; } = string.Empty;
        /// <summary>开始时间</summary>
        public DateTimeOffset StartedAt { get; set; }
        /// <summary>结束时间</summary>
        public DateTimeOffset EndedAt { get; set; }
        /// <summary>整体结果等级</summary>
        public ResultLevel OverallLevel { get; set; }
        /// <summary>整体严重级别</summary>
        public RuleSeverity OverallSeverity { get; set; }
        /// <summary>是否允许投产</summary>
        public bool AllowProduction { get; set; }
        /// <summary>汇总信息</summary>
        public DiagnosticSummary Summary { get; set; } = new DiagnosticSummary();
        /// <summary>每条规则的详细结果</summary>
        public List<DiagnosticRuleResult> Results { get; set; } = new List<DiagnosticRuleResult>();
    }

    /// <summary>
    /// 规则元数据定义（用于规则目录 RuleCatalog 注册）
    /// </summary>
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
