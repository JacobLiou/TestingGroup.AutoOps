using System;

namespace SelfDiagnostic.Models
{
    /// <summary>
    /// 向 MIMS 上报或查询用的 ASK 信息请求载荷
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
    /// 请求 MIMS 环境配置（工位/产线等）的请求模型
    /// </summary>
    public sealed class MimsEnvironmentConfigRequest
    {
        public string StationId { get; set; } = "STATION-001";
        public string LineId { get; set; } = "LINE-001";
    }

    /// <summary>
    /// 调用外部端点发送后的通用结果
    /// </summary>
    public sealed class ExternalSendResult
    {
        public bool Success { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
    }

    /// <summary>
    /// MIMS 环境配置请求的返回结果（含配置 XML）
    /// </summary>
    public sealed class MimsEnvironmentConfigResult
    {
        public bool Success { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public string ConfigXml { get; set; } = string.Empty;
    }
}
