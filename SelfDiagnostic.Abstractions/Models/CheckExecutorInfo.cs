namespace SelfDiagnostic.Models
{
    /// <summary>
    /// 检查执行器描述信息，用于方法浏览器/选择器中展示可绑定的方法列表
    /// </summary>
    public sealed class CheckExecutorInfo
    {
        /// <summary>检查项 ID（来自 [CheckExecutor] 特性或 RunBook 配置）</summary>
        public string CheckId { get; set; } = string.Empty;

        /// <summary>检查项显示名称</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>方法/检查项描述</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>默认分类（如 SystemCheck、StationCheck 等）</summary>
        public string DefaultCategory { get; set; } = "SystemCheck";

        /// <summary>方法名（不含类名）</summary>
        public string MethodName { get; set; } = string.Empty;

        /// <summary>所在类的完全限定名（Namespace.ClassName）</summary>
        public string ClassName { get; set; } = string.Empty;

        /// <summary>所属插件程序集名称（不含 .dll 后缀）</summary>
        public string PluginAssembly { get; set; } = string.Empty;

        /// <summary>绑定 DLL 文件名（程序集名称 + ".dll"）</summary>
        public string BindDll
        {
            get { return string.IsNullOrEmpty(PluginAssembly) ? string.Empty : PluginAssembly + ".dll"; }
        }

        /// <summary>绑定方法全路径（ClassName.MethodName）</summary>
        public string BindMethod
        {
            get { return string.IsNullOrEmpty(ClassName) ? string.Empty : ClassName + "." + MethodName; }
        }
    }
}
