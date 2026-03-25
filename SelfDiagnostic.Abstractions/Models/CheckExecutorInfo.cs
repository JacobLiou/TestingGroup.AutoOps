namespace SelfDiagnostic.Models
{
    public sealed class CheckExecutorInfo
    {
        public string CheckId { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DefaultCategory { get; set; } = "SystemCheck";
        public string MethodName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string PluginAssembly { get; set; } = string.Empty;

        public string BindDll
        {
            get { return string.IsNullOrEmpty(PluginAssembly) ? string.Empty : PluginAssembly + ".dll"; }
        }

        public string BindMethod
        {
            get { return string.IsNullOrEmpty(ClassName) ? string.Empty : ClassName + "." + MethodName; }
        }
    }
}
