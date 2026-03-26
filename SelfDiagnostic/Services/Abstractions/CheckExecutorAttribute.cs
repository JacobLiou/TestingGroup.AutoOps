using System;

namespace SelfDiagnostic.Services.Abstractions
{
    /// <summary>
    /// 标记一个方法为诊断检查执行器的特性（主项目副本）。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class CheckExecutorAttribute : Attribute
    {
        /// <summary>
        /// 使用指定的检查项 ID 初始化特性。
        /// </summary>
        public CheckExecutorAttribute(string checkId)
        {
            CheckId = checkId;
        }

        /// <summary>
        /// 诊断检查项的唯一标识符。
        /// </summary>
        public string CheckId { get; }
    }
}
