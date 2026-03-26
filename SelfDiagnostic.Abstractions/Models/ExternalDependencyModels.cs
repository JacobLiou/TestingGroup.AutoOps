using System.Collections.Generic;

namespace SelfDiagnostic.Models
{
    /// <summary>
    /// 外部依赖系统的检查项 ID 常量
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
    /// 外部依赖端点配置（单个服务的 URL 及名称）
    /// </summary>
    public sealed class ExternalDependencyEndpoint
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    /// <summary>
    /// 外部依赖完整配置（从 JSON 文件加载，包含所有端点）
    /// </summary>
    public sealed class ExternalDependencyConfig
    {
        public Dictionary<string, ExternalDependencyEndpoint> Endpoints { get; set; }
            = new Dictionary<string, ExternalDependencyEndpoint>();
    }

    /// <summary>
    /// 单个外部依赖端点的 HTTP 探测结果
    /// </summary>
    public sealed class ExternalDependencyCheckResult
    {
        public bool Success { get; set; }
        public int? StatusCode { get; set; }
        /// <summary>响应耗时（毫秒）</summary>
        public long ElapsedMs { get; set; }
        public string Error { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string EndpointName { get; set; } = string.Empty;
    }

    /// <summary>
    /// 诊断运行上下文 — 在一次扫描过程中由引擎构建，供所有 Step 共享。
    /// 包含外部配置、TP 连通性快照、工站能力要求、电源质量要求等。
    /// </summary>
    public sealed class DiagnosticRunContext
    {
        /// <summary>外部检查是否可用（MIMS 配置是否获取成功）</summary>
        public bool ExternalChecksEnabled { get; set; }

        /// <summary>外部依赖端点配置</summary>
        public ExternalDependencyConfig ExternalConfig { get; set; }

        /// <summary>配置来源描述（如 "MIMS(http://...)"）</summary>
        public string ConfigSource { get; set; } = string.Empty;

        /// <summary>配置获取失败时的错误信息</summary>
        public string ConfigError { get; set; } = string.Empty;

        /// <summary>TP 连通性检测快照</summary>
        public TpConnectivitySnapshot TpConnectivity { get; set; }

        /// <summary>工站能力基线要求（来自 MIMS 配置）</summary>
        public StationCapabilityRequirements StationCapabilityRequirements { get; set; }

        /// <summary>电源质量基线要求（来自 MIMS 配置）</summary>
        public PowerSupplyRequirements PowerSupplyRequirements { get; set; }

        /// <summary>MIMS 返回的原始 XML 配置（供自定义解析）</summary>
        public string RawMimsConfigXml { get; set; } = string.Empty;
    }
}
