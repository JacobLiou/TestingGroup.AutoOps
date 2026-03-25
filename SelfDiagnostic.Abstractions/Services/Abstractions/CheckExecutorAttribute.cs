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
    }
}
