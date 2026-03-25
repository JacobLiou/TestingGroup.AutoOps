using System;

namespace SelfDiagnostic.Services.Abstractions
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class CheckExecutorAttribute : Attribute
    {
        public CheckExecutorAttribute(string checkId)
        {
            CheckId = checkId;
        }

        public string CheckId { get; }
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DefaultCategory { get; set; } = "SystemCheck";
    }
}
