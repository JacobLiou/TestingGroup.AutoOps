using System;

namespace SelfDiagnostic.Models
{
    /// <summary>
    /// 向 MIMS 发送 AskInfo 请求的参数模型
    /// </summary>
    public sealed class MimsAskInfoRequest
    {
        public string Author { get; set; } = "YUD";
        public string Spec { get; set; } = "GUI";
        public string PartNumber { get; set; } = "TEST-001";
        public DateTime Date { get; set; } = DateTime.Now;
        public int TotalItems { get; set; }
        public int PassCount { get; set; }
        public int WarningCount { get; set; }
        public int FailCount { get; set; }
    }

    /// <summary>
    /// 向 MIMS 请求环境配置的参数模型
    /// </summary>
    public sealed class MimsEnvironmentConfigRequest
    {
        /// <summary>工站 ID</summary>
        public string StationId { get; set; } = "STATION-001";
        /// <summary>产线 ID</summary>
        public string LineId { get; set; } = "LINE-001";
    }

    /// <summary>
    /// MIMS 通用发送操作的结果
    /// </summary>
    public sealed class ExternalSendResult
    {
        public bool Success { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        /// <summary>实际通信端点地址</summary>
        public string Endpoint { get; set; } = string.Empty;
    }

    /// <summary>
    /// MIMS 环境配置请求的返回结果（包含 XML 配置内容）
    /// </summary>
    public sealed class MimsEnvironmentConfigResult
    {
        public bool Success { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        /// <summary>MIMS 返回的环境配置 XML</summary>
        public string ConfigXml { get; set; } = string.Empty;
    }
}
