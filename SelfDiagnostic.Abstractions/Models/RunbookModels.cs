using System.Collections.Generic;

namespace SelfDiagnostic.Models
{
    /// <summary>
    /// RunBook 定义 — 一份完整的诊断流程配置，包含若干有序步骤。
    /// 从 JSON 文件反序列化而来（如 default.runbook.json）。
    /// </summary>
    public sealed class RunbookDefinition
    {
        /// <summary>RunBook 唯一标识</summary>
        public string Id { get; set; } = "default";

        /// <summary>RunBook 标题</summary>
        public string Title { get; set; } = "默认诊断 RunBook";

        /// <summary>RunBook 版本号</summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>诊断步骤有序列表</summary>
        public List<RunbookStepDefinition> Steps { get; set; } = new List<RunbookStepDefinition>();
    }

    /// <summary>
    /// RunBook 中单个步骤的定义 — 描述要执行的检查项及其绑定的 DLL 方法。
    /// </summary>
    public sealed class RunbookStepDefinition
    {
        /// <summary>检查项 ID（必须全局唯一）</summary>
        public string CheckId { get; set; } = string.Empty;

        /// <summary>显示名称</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>所属分类名称（对应 CheckCategory 枚举的字符串形式）</summary>
        public string Category { get; set; } = nameof(CheckCategory.SystemCheck);

        /// <summary>绑定的 DLL 文件名（如 SelfDiagnostic.Checks.System.dll）</summary>
        public string BindDll { get; set; } = string.Empty;

        /// <summary>绑定的方法全路径（Namespace.ClassName.MethodName）</summary>
        public string BindMethod { get; set; } = string.Empty;

        /// <summary>执行超时时间（毫秒），默认 5000ms</summary>
        public int TimeoutMs { get; set; } = 5000;

        /// <summary>是否启用此步骤</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>传递给方法的参数字典（键值对均为字符串，由 GenericMethodInvoker 自动转换类型）</summary>
        public Dictionary<string, string> Params { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// 检查执行结果（通用返回值，由引擎或 GenericMethodInvoker 构造）
    /// </summary>
    public sealed class CheckExecutionOutcome
    {
        /// <summary>执行是否成功</summary>
        public bool Success { get; set; }
    }
}