using System;

namespace SelfDiagnostic.Services.Abstractions
{
    /// <summary>
    /// 标记一个方法为诊断检查执行器。
    /// 引擎启动时通过反射扫描所有标注此特性的方法，自动注册到 CheckExecutorRegistry。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class CheckExecutorAttribute : Attribute
    {
        public CheckExecutorAttribute(string checkId)
        {
            CheckId = checkId;
        }

        /// <summary>检查项唯一 ID（必须与 RunBook 中的 CheckId 对应）</summary>
        public string CheckId { get; }

        /// <summary>检查项显示名称（可选）</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>方法描述（可选，用于 UI 展示）</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>默认分类（可选，默认 SystemCheck）</summary>
        public string DefaultCategory { get; set; } = "SystemCheck";
    }
}