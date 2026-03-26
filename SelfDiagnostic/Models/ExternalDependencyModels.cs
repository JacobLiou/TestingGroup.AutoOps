using System.Collections.Generic;

namespace SelfDiagnostic.Models
{
    /// <summary>
    /// 外部依赖系统 ID 常量
    /// </summary>
    public static class ExternalDependencyIds
    {
        public const string Mes = "EXT_MES";
        public const string Tms = "EXT_TMS";
        public const string Tas = "EXT_TAS";
        public const string FileServer = "EXT_FILE_SERVER";
        public const string Lan = "EXT_LAN";
    }

    /// <summary>
    /// 外部依赖端点配置
    /// </summary>
    public sealed class ExternalDependencyEndpoint
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    /// <summary>
    /// 外部依赖完整配置
    /// </summary>
    public sealed class ExternalDependencyConfig
    {
        public Dictionary<string, ExternalDependencyEndpoint> Endpoints { get; set; }
            = new Dictionary<string, ExternalDependencyEndpoint>();
    }

    /// <summary>
    /// 外部依赖探测结果
    /// </summary>
    public sealed class ExternalDependencyCheckResult
    {
        public bool Success { get; set; }
        public int? StatusCode { get; set; }
        public long ElapsedMs { get; set; }
        public string Error { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string EndpointName { get; set; } = string.Empty;
    }

    /// <summary>
    /// 诊断运行上下文
    /// </summary>
    public sealed class DiagnosticRunContext
    {
        public bool ExternalChecksEnabled { get; set; }
        public ExternalDependencyConfig ExternalConfig { get; set; }
        public string ConfigSource { get; set; } = string.Empty;
        public string ConfigError { get; set; } = string.Empty;
        public TpConnectivitySnapshot TpConnectivity { get; set; }
        public StationCapabilityRequirements StationCapabilityRequirements { get; set; }
        public PowerSupplyRequirements PowerSupplyRequirements { get; set; }
        public string RawMimsConfigXml { get; set; } = string.Empty;
    }
}
